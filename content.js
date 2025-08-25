(function() {
  const originalApply = Function.prototype.apply;
  const originalCall = Function.prototype.call;
  
  function logFunctionCall(functionName, args, source) {
    const callData = {
      functionName: functionName,
      arguments: Array.from(args).map(arg => {
        try {
          if (typeof arg === 'function') {
            return `[Function: ${arg.name || 'anonymous'}]`;
          }
          if (typeof arg === 'object') {
            if (arg === null) return 'null';
            if (arg === window) return '[Window]';
            if (arg === document) return '[Document]';
            return JSON.stringify(arg, null, 2);
          }
          return String(arg);
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
    const callbackName = callback.name || 'anonymous_timeout_callback';
    logFunctionCall('setTimeout', [callbackName, delay, ...args], 'setTimeout');
    return originalSetTimeout.apply(this, arguments);
  };

  window.setInterval = function(callback, delay, ...args) {
    const callbackName = callback.name || 'anonymous_interval_callback';
    logFunctionCall('setInterval', [callbackName, delay, ...args], 'setInterval');
    return originalSetInterval.apply(this, arguments);
  };

  window.addEventListener = function(type, listener, options) {
    const listenerName = listener.name || 'anonymous_listener';
    logFunctionCall('addEventListener', [type, listenerName, options], 'addEventListener');
    return originalAddEventListener.apply(this, arguments);
  };

  const originalFetch = window.fetch;
  if (originalFetch) {
    window.fetch = function(...args) {
      logFunctionCall('fetch', args, 'fetch');
      return originalFetch.apply(this, args);
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

  // 간단한 클릭 이벤트만 감지 (성능 최적화)
  document.addEventListener('click', function(event) {
    const target = event.target;
    logFunctionCall('user_click', [target.tagName, target.id || 'no-id', target.className || 'no-class'], 'user-interaction');
  }, true);

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