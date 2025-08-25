chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'FUNCTION_CALL') {
    const callData = {
      ...request.data,
      tabId: sender.tab.id,
      tabUrl: sender.tab.url,
      id: Date.now() + Math.random()
    };
    
    chrome.storage.local.get(['functionCalls'], (result) => {
      const existingCalls = result.functionCalls || [];
      existingCalls.push(callData);
      
      if (existingCalls.length > 1000) {
        existingCalls.splice(0, existingCalls.length - 1000);
      }
      
      chrome.storage.local.set({ functionCalls: existingCalls }, () => {
        console.log('Function call stored:', callData);
      });
    });
    
    sendResponse({ status: 'stored' });
  }
  
  if (request.type === 'GET_FUNCTION_CALLS') {
    chrome.storage.local.get(['functionCalls'], (result) => {
      sendResponse({ functionCalls: result.functionCalls || [] });
    });
    return true;
  }
  
  if (request.type === 'CLEAR_FUNCTION_CALLS') {
    chrome.storage.local.set({ functionCalls: [] }, () => {
      sendResponse({ status: 'cleared' });
    });
    return true;
  }
});