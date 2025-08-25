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

  // 클릭 이벤트 감지
  document.addEventListener('click', function(event) {
    const target = event.target;
    const tagName = target.tagName;
    const id = target.id;
    const className = target.className;
    const onclick = target.onclick;
    
    if (onclick) {
      logFunctionCall('click_handler', [tagName, id, className, onclick.toString()], 'user-click');
    } else {
      logFunctionCall('click_event', [tagName, id, className], 'user-click');
    }
  }, true);

  // 폼 제출 감지
  document.addEventListener('submit', function(event) {
    const form = event.target;
    const formData = new FormData(form);
    const formValues = {};
    for (let [key, value] of formData.entries()) {
      formValues[key] = value;
    }
    logFunctionCall('form_submit', [form.id, form.className, formValues], 'user-submit');
  }, true);

  // 입력 변경 감지
  document.addEventListener('change', function(event) {
    const target = event.target;
    if (target.tagName === 'INPUT' || target.tagName === 'SELECT' || target.tagName === 'TEXTAREA') {
      logFunctionCall('input_change', [target.tagName, target.name, target.id, target.value], 'user-input');
    }
  }, true);

  // eval과 Function 생성자 감지
  const originalEval = window.eval;
  const originalFunction = window.Function;
  
  window.eval = function(code) {
    logFunctionCall('eval', [code], 'eval');
    return originalEval.call(this, code);
  };
  
  window.Function = function(...args) {
    logFunctionCall('Function_constructor', args, 'constructor');
    return originalFunction.apply(this, args);
  };

  // 스크립트 태그를 통한 함수 정의 감지
  const originalCreateElement = document.createElement;
  document.createElement = function(tagName) {
    const element = originalCreateElement.call(this, tagName);
    
    if (tagName.toLowerCase() === 'script') {
      const originalSetAttribute = element.setAttribute;
      element.setAttribute = function(name, value) {
        if (name === 'src') {
          logFunctionCall('script_src_set', [value], 'script-creation');
        }
        return originalSetAttribute.call(this, name, value);
      };
      
      const originalTextContentSetter = Object.getOwnPropertyDescriptor(Node.prototype, 'textContent').set;
      Object.defineProperty(element, 'textContent', {
        set: function(value) {
          if (value && value.trim()) {
            logFunctionCall('inline_script', [value.substring(0, 200) + '...'], 'script-creation');
          }
          return originalTextContentSetter.call(this, value);
        },
        get: Object.getOwnPropertyDescriptor(Node.prototype, 'textContent').get
      });
    }
    
    return element;
  };

  // DOM에 추가된 요소들의 이벤트 핸들러 감지
  const observer = new MutationObserver(mutations => {
    mutations.forEach(mutation => {
      if (mutation.type === 'childList') {
        mutation.addedNodes.forEach(node => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.tagName === 'SCRIPT' && node.src) {
              logFunctionCall('script_loaded', [node.src], 'DOM');
            }
            
            // 새로 추가된 요소의 onclick 핸들러 감지
            if (node.onclick) {
              logFunctionCall('new_element_handler', [node.tagName, node.id, node.onclick.toString()], 'DOM');
            }
            
            // 자식 요소들의 핸들러도 확인
            const elementsWithHandlers = node.querySelectorAll ? node.querySelectorAll('[onclick]') : [];
            elementsWithHandlers.forEach(el => {
              logFunctionCall('nested_element_handler', [el.tagName, el.id, el.onclick.toString()], 'DOM');
            });
          }
        });
      }
    });
  });

  observer.observe(document, {
    childList: true,
    subtree: true
  });

  // 페이지 로드 후 기존 요소들의 핸들러 스캔
  window.addEventListener('DOMContentLoaded', function() {
    const elementsWithHandlers = document.querySelectorAll('[onclick]');
    elementsWithHandlers.forEach(el => {
      logFunctionCall('existing_element_handler', [el.tagName, el.id, el.onclick.toString()], 'DOM-scan');
    });
  });
})();