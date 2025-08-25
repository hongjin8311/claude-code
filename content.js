(function() {
  const originalFunctions = new Map();
  
  function serializeArgument(arg, maxDepth = 2, currentDepth = 0) {
    if (currentDepth > maxDepth) return '[Max Depth Reached]';
    
    try {
      if (arg === null) return 'null';
      if (arg === undefined) return 'undefined';
      if (typeof arg === 'function') return `[Function: ${arg.name || 'anonymous'}]`;
      if (typeof arg === 'symbol') return arg.toString();
      if (arg instanceof Error) return `[Error: ${arg.message}]`;
      if (arg instanceof Date) return `[Date: ${arg.toISOString()}]`;
      if (arg instanceof RegExp) return `[RegExp: ${arg.toString()}]`;
      
      if (typeof arg === 'object') {
        if (arg === window) return '[Window]';
        if (arg === document) return '[Document]';
        if (arg.nodeType) return `[${arg.nodeName}: ${arg.id || arg.className || 'element'}]`;
        
        const seen = new WeakSet();
        if (seen.has(arg)) return '[Circular Reference]';
        seen.add(arg);
        
        if (Array.isArray(arg)) {
          return arg.length > 10 ? `[Array(${arg.length}): ${arg.slice(0, 3).map(item => serializeArgument(item, maxDepth, currentDepth + 1)).join(', ')}...]` :
                 `[Array: ${arg.map(item => serializeArgument(item, maxDepth, currentDepth + 1)).join(', ')}]`;
        }
        
        const keys = Object.keys(arg);
        if (keys.length > 5) {
          return `[Object: {${keys.slice(0, 3).join(', ')}...}]`;
        }
        return `[Object: ${JSON.stringify(arg)}]`;
      }
      
      return typeof arg === 'string' && arg.length > 100 ? 
             `"${arg.substring(0, 100)}..."` : 
             String(arg);
    } catch (e) {
      return '[Serialization Error]';
    }
  }
  
  function logFunctionCall(functionName, args, source, context = '') {
    try {
      const callData = {
        functionName: functionName,
        arguments: Array.from(args || []).map(arg => serializeArgument(arg)),
        timestamp: new Date().toISOString(),
        url: window.location.href,
        source: source,
        context: context
      };
      
      chrome.runtime.sendMessage({
        type: 'FUNCTION_CALL',
        data: callData
      });
    } catch (e) {
      console.warn('Function call logging failed:', e);
    }
  }

  function createProxy(target, name, source) {
    if (!target || typeof target !== 'function') return target;
    
    return new Proxy(target, {
      apply: function(targetFunc, thisArg, argumentsList) {
        const realName = targetFunc.name || name || 'anonymous';
        logFunctionCall(realName, argumentsList, source, thisArg?.constructor?.name || '');
        return targetFunc.apply(thisArg, argumentsList);
      }
    });
  }

  function hookGlobalFunctions() {
    const globalFunctions = [
      'setTimeout', 'setInterval', 'clearTimeout', 'clearInterval',
      'fetch', 'eval', 'parseInt', 'parseFloat', 'isNaN', 'isFinite',
      'encodeURI', 'decodeURI', 'encodeURIComponent', 'decodeURIComponent',
      'alert', 'confirm', 'prompt'
    ];
    
    globalFunctions.forEach(funcName => {
      if (window[funcName] && typeof window[funcName] === 'function') {
        const original = window[funcName];
        originalFunctions.set(funcName, original);
        window[funcName] = createProxy(original, funcName, 'global');
      }
    });
  }

  function hookConsoleMethods() {
    if (typeof console !== 'undefined') {
      ['log', 'warn', 'error', 'info', 'debug', 'trace', 'assert', 'group', 'groupCollapsed', 'groupEnd'].forEach(method => {
        if (console[method] && typeof console[method] === 'function') {
          const original = console[method];
          originalFunctions.set(`console.${method}`, original);
          console[method] = createProxy(original, method, 'console');
        }
      });
    }
  }

  function hookEventListeners() {
    const eventTargets = [window, document, document.body];
    const eventMethods = ['addEventListener', 'removeEventListener'];
    
    eventTargets.forEach(target => {
      if (!target) return;
      eventMethods.forEach(method => {
        if (target[method] && typeof target[method] === 'function') {
          const original = target[method];
          const key = `${target.constructor.name}.${method}`;
          if (!originalFunctions.has(key)) {
            originalFunctions.set(key, original);
            target[method] = createProxy(original, method, 'event');
          }
        }
      });
    });
  }

  function hookPrototypeMethods() {
    const prototypes = [
      { obj: Function.prototype, methods: ['call', 'apply', 'bind'] },
      { obj: Array.prototype, methods: ['forEach', 'map', 'filter', 'reduce', 'find'] },
      { obj: Promise.prototype, methods: ['then', 'catch', 'finally'] }
    ];
    
    prototypes.forEach(({ obj, methods }) => {
      methods.forEach(method => {
        if (obj[method] && typeof obj[method] === 'function') {
          const original = obj[method];
          const key = `${obj.constructor.name}.${method}`;
          if (!originalFunctions.has(key)) {
            originalFunctions.set(key, original);
            obj[method] = function(...args) {
              const funcName = this.name || method;
              logFunctionCall(funcName, args, 'prototype', method);
              return original.apply(this, args);
            };
          }
        }
      });
    });
  }

  function interceptObjectAccess() {
    const originalDescriptor = Object.getOwnPropertyDescriptor;
    Object.getOwnPropertyDescriptor = function(obj, prop) {
      try {
        if (obj && typeof obj[prop] === 'function' && prop !== 'constructor') {
          logFunctionCall(`${obj.constructor.name}.${prop}`, [], 'property-access');
        }
      } catch (e) {}
      return originalDescriptor.apply(this, arguments);
    };
  }

  function observeScriptExecution() {
    const observer = new MutationObserver(mutations => {
      mutations.forEach(mutation => {
        if (mutation.type === 'childList') {
          mutation.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) {
              if (node.tagName === 'SCRIPT') {
                const src = node.src || 'inline';
                logFunctionCall('script_execution', [src], 'DOM', 'script-load');
              }
            }
          });
        }
      });
    });

    observer.observe(document, {
      childList: true,
      subtree: true
    });
  }

  window.addEventListener('beforeunload', () => {
    logFunctionCall('page_unload', [window.location.href], 'navigation');
  });

  window.addEventListener('load', () => {
    logFunctionCall('page_loaded', [window.location.href], 'navigation');
  });

  hookGlobalFunctions();
  hookConsoleMethods();
  hookEventListeners();
  hookPrototypeMethods();
  interceptObjectAccess();
  observeScriptExecution();

  console.log('Function Call Monitor: Enhanced monitoring active');
})();