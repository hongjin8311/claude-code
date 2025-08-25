(function() {
  const originalFunctions = new Map();
  let isMonitoring = true;
  
  function serializeArgument(arg, maxDepth = 1, currentDepth = 0) {
    if (currentDepth > maxDepth) return '[Max Depth]';
    
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
        if (arg.nodeType) return `[Element: ${arg.nodeName}]`;
        
        if (Array.isArray(arg)) {
          return arg.length > 5 ? `[Array(${arg.length})]` : `[Array: ${arg.slice(0, 3).join(', ')}]`;
        }
        
        const keys = Object.keys(arg);
        if (keys.length > 3) {
          return `[Object: {${keys.slice(0, 2).join(', ')}...}]`;
        }
        return `[Object: {${keys.join(', ')}}]`;
      }
      
      return typeof arg === 'string' && arg.length > 50 ? 
             `"${arg.substring(0, 50)}..."` : 
             String(arg);
    } catch (e) {
      return '[Error]';
    }
  }
  
  function logFunctionCall(functionName, args, source) {
    if (!isMonitoring) return;
    
    try {
      if (functionName.includes('chrome') || functionName.includes('extension')) return;
      
      const callData = {
        functionName: functionName,
        arguments: Array.from(args || []).slice(0, 5).map(arg => serializeArgument(arg)),
        timestamp: new Date().toISOString(),
        url: window.location.href,
        source: source
      };
      
      setTimeout(() => {
        chrome.runtime.sendMessage({
          type: 'FUNCTION_CALL',
          data: callData
        }).catch(() => {});
      }, 0);
    } catch (e) {}
  }

  function safeHook(original, name, source) {
    if (!original || typeof original !== 'function') return original;
    
    return function(...args) {
      try {
        logFunctionCall(name, args, source);
      } catch (e) {}
      return original.apply(this, args);
    };
  }

  function hookSelectiveFunctions() {
    const safeFunctions = ['setTimeout', 'setInterval', 'fetch', 'alert', 'confirm'];
    
    safeFunctions.forEach(funcName => {
      if (window[funcName] && typeof window[funcName] === 'function') {
        const original = window[funcName];
        originalFunctions.set(funcName, original);
        window[funcName] = safeHook(original, funcName, 'global');
      }
    });
  }

  function hookConsoleMethods() {
    if (typeof console !== 'undefined') {
      ['log', 'warn', 'error'].forEach(method => {
        if (console[method] && typeof console[method] === 'function') {
          const original = console[method];
          originalFunctions.set(`console.${method}`, original);
          console[method] = safeHook(original, method, 'console');
        }
      });
    }
  }

  function observeUserInteractions() {
    const events = ['click', 'submit', 'change'];
    
    events.forEach(eventType => {
      document.addEventListener(eventType, (e) => {
        logFunctionCall(`${eventType}_event`, [e.target.tagName, e.target.id || e.target.className], 'user-interaction');
      }, true);
    });
  }

  function observeScripts() {
    const observer = new MutationObserver(mutations => {
      mutations.forEach(mutation => {
        if (mutation.type === 'childList') {
          mutation.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE && node.tagName === 'SCRIPT' && node.src) {
              logFunctionCall('script_loaded', [node.src], 'DOM');
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

  window.addEventListener('DOMContentLoaded', () => {
    logFunctionCall('dom_loaded', [window.location.href], 'navigation');
  });

  hookSelectiveFunctions();
  hookConsoleMethods();
  observeUserInteractions();
  observeScripts();

  window.toggleMonitoring = () => {
    isMonitoring = !isMonitoring;
    console.log('Function monitoring:', isMonitoring ? 'enabled' : 'disabled');
  };

  console.log('Function Call Monitor: Safe monitoring active');
})();