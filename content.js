(function() {
  const originalFunction = window.Function.prototype.constructor;
  const originalApply = Function.prototype.apply;
  const originalCall = Function.prototype.call;
  
  function logFunctionCall(functionName, args, source) {
    const callData = {
      functionName: functionName,
      arguments: Array.from(args).map(arg => {
        try {
          return typeof arg === 'function' ? '[Function]' : 
                 typeof arg === 'object' ? JSON.stringify(arg, null, 2) : 
                 String(arg);
        } catch (e) {
          return '[Circular Reference]';
        }
      }),
      timestamp: new Date().toISOString(),
      url: window.location.href,
      source: source
    };
    
    chrome.runtime.sendMessage({
      type: 'FUNCTION_CALL',
      data: callData
    });
  }

  Function.prototype.apply = function(thisArg, args) {
    const funcName = this.name || 'anonymous';
    logFunctionCall(funcName, args || [], 'apply');
    return originalApply.call(this, thisArg, args);
  };

  Function.prototype.call = function(thisArg, ...args) {
    const funcName = this.name || 'anonymous';
    logFunctionCall(funcName, args, 'call');
    return originalCall.apply(this, [thisArg, ...args]);
  };

  const originalSetTimeout = window.setTimeout;
  const originalSetInterval = window.setInterval;
  const originalAddEventListener = window.addEventListener;

  window.setTimeout = function(callback, delay, ...args) {
    logFunctionCall('setTimeout', [callback.toString(), delay, ...args], 'setTimeout');
    return originalSetTimeout.apply(this, arguments);
  };

  window.setInterval = function(callback, delay, ...args) {
    logFunctionCall('setInterval', [callback.toString(), delay, ...args], 'setInterval');
    return originalSetInterval.apply(this, arguments);
  };

  window.addEventListener = function(type, listener, options) {
    logFunctionCall('addEventListener', [type, listener.toString(), options], 'addEventListener');
    return originalAddEventListener.apply(this, arguments);
  };

  const originalFetch = window.fetch;
  if (originalFetch) {
    window.fetch = function(...args) {
      logFunctionCall('fetch', args, 'fetch');
      return originalFetch.apply(this, args);
    };
  }

  const originalXMLHttpRequest = window.XMLHttpRequest;
  if (originalXMLHttpRequest) {
    window.XMLHttpRequest = function(...args) {
      logFunctionCall('XMLHttpRequest', args, 'XMLHttpRequest');
      return new originalXMLHttpRequest(...args);
    };
  }

  const hookMethod = (obj, methodName) => {
    if (obj && obj[methodName] && typeof obj[methodName] === 'function') {
      const original = obj[methodName];
      obj[methodName] = function(...args) {
        logFunctionCall(methodName, args, `${obj.constructor.name}.${methodName}`);
        return original.apply(this, args);
      };
    }
  };

  if (typeof console !== 'undefined') {
    ['log', 'warn', 'error', 'info', 'debug'].forEach(method => {
      hookMethod(console, method);
    });
  }

  const observer = new MutationObserver(mutations => {
    mutations.forEach(mutation => {
      if (mutation.type === 'childList') {
        mutation.addedNodes.forEach(node => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.tagName === 'SCRIPT' && node.src) {
              logFunctionCall('script_loaded', [node.src], 'DOM');
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
})();