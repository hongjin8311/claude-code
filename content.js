(function() {
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
            if (arg && arg.constructor) return `[${arg.constructor.name}]`;
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

  // 전역 window 객체의 함수들을 동적으로 후킹
  function hookWindowFunctions() {
    const windowFunctions = [];
    for (let prop in window) {
      try {
        if (typeof window[prop] === 'function' && 
            !prop.startsWith('chrome') && 
            !prop.startsWith('webkit') &&
            !['setTimeout', 'setInterval', 'addEventListener', 'fetch'].includes(prop)) {
          windowFunctions.push(prop);
        }
      } catch (e) {}
    }
    
    windowFunctions.forEach(funcName => {
      try {
        const original = window[funcName];
        window[funcName] = function(...args) {
          logFunctionCall(funcName, args, 'global-function');
          return original.apply(this, args);
        };
      } catch (e) {}
    });
  }

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

  // 사용자 정의 함수 호출을 감지하기 위한 eval 후킹
  const originalEval = window.eval;
  window.eval = function(code) {
    // 함수 정의나 함수 호출이 포함된 코드인지 확인
    if (typeof code === 'string' && (code.includes('function') || code.includes('('))) {
      logFunctionCall('eval_execution', [code.substring(0, 100) + '...'], 'eval');
    }
    return originalEval.call(this, code);
  };

  // 클릭 이벤트에서 onclick 속성의 함수 호출 감지
  document.addEventListener('click', function(event) {
    const target = event.target;
    
    // onclick 속성이 있는 경우 함수 내용 추출
    if (target.onclick) {
      const onclickStr = target.onclick.toString();
      logFunctionCall('onclick_handler', [target.tagName, target.id, onclickStr], 'user-click');
    } else {
      logFunctionCall('user_click', [target.tagName, target.id || 'no-id', target.className || 'no-class'], 'user-interaction');
    }
  }, true);

  // 페이지의 모든 스크립트 태그를 관찰하여 인라인 스크립트 감지
  function scanInlineScripts() {
    const scripts = document.querySelectorAll('script:not([src])');
    scripts.forEach((script, index) => {
      if (script.textContent && script.textContent.trim()) {
        const content = script.textContent.trim();
        // 함수 호출이나 정의가 있는 스크립트만 로그
        if (content.includes('(') && content.includes(')')) {
          logFunctionCall('inline_script_execution', [`Script_${index}`, content.substring(0, 200) + '...'], 'inline-script');
        }
      }
    });
  }

  // DOM이 로드된 후 기존 스크립트들 스캔
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', scanInlineScripts);
  } else {
    scanInlineScripts();
  }

  // 새로 추가되는 스크립트 감지
  const observer = new MutationObserver(mutations => {
    mutations.forEach(mutation => {
      if (mutation.type === 'childList') {
        mutation.addedNodes.forEach(node => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.tagName === 'SCRIPT') {
              if (node.src) {
                logFunctionCall('external_script_loaded', [node.src], 'DOM');
              } else if (node.textContent && node.textContent.trim()) {
                const content = node.textContent.trim();
                if (content.includes('(') && content.includes(')')) {
                  logFunctionCall('dynamic_inline_script', [content.substring(0, 200) + '...'], 'DOM');
                }
              }
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

  // 전역 함수들 후킹 실행
  setTimeout(hookWindowFunctions, 100);
})();