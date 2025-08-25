document.addEventListener('DOMContentLoaded', function() {
  const callList = document.getElementById('callList');
  const refreshBtn = document.getElementById('refresh');
  const clearBtn = document.getElementById('clear');
  const exportBtn = document.getElementById('export');

  function formatArguments(args) {
    if (!args || args.length === 0) return '()';
    
    return '(' + args.map(arg => {
      if (typeof arg === 'string' && arg.length > 100) {
        return `"${arg.substring(0, 100)}..."`;
      }
      return arg;
    }).join(', ') + ')';
  }

  function displayFunctionCalls(calls) {
    if (!calls || calls.length === 0) {
      callList.innerHTML = '<div class="no-calls">저장된 함수 호출이 없습니다</div>';
      return;
    }

    const html = calls.slice(-50).reverse().map(call => `
      <div class="call-item">
        <div class="call-name">${call.functionName || 'anonymous'}</div>
        <div class="call-args">${formatArguments(call.arguments)}</div>
        <div class="call-meta">
          ${new Date(call.timestamp).toLocaleString()} | 
          ${call.source} | 
          ${call.url ? new URL(call.url).hostname : 'unknown'}
        </div>
      </div>
    `).join('');

    callList.innerHTML = html;
  }

  function loadFunctionCalls() {
    chrome.runtime.sendMessage({ type: 'GET_FUNCTION_CALLS' }, (response) => {
      displayFunctionCalls(response.functionCalls);
    });
  }

  function clearFunctionCalls() {
    if (confirm('모든 저장된 함수 호출을 삭제하시겠습니까?')) {
      chrome.runtime.sendMessage({ type: 'CLEAR_FUNCTION_CALLS' }, (response) => {
        loadFunctionCalls();
      });
    }
  }

  function exportFunctionCalls() {
    chrome.runtime.sendMessage({ type: 'GET_FUNCTION_CALLS' }, (response) => {
      const calls = response.functionCalls || [];
      const dataStr = JSON.stringify(calls, null, 2);
      const dataBlob = new Blob([dataStr], { type: 'application/json' });
      const url = URL.createObjectURL(dataBlob);
      
      const link = document.createElement('a');
      link.href = url;
      link.download = `function-calls-${new Date().toISOString().split('T')[0]}.json`;
      link.click();
      
      URL.revokeObjectURL(url);
    });
  }

  refreshBtn.addEventListener('click', loadFunctionCalls);
  clearBtn.addEventListener('click', clearFunctionCalls);
  exportBtn.addEventListener('click', exportFunctionCalls);

  loadFunctionCalls();

  setInterval(loadFunctionCalls, 2000);
});