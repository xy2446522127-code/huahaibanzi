function buildExpression(operation) {
  if (operation === 'restore-background') {
    return `(async () => {
      let toggle=null;
      for(let i=0;i<100;i++){
        toggle=document.querySelector('#backgroundToggle');
        if(toggle)break;
        await new Promise(resolve=>setTimeout(resolve,100));
      }
      if (!toggle) return { restored: false, reason: 'missing-toggle-timeout' };
      if (!toggle.classList.contains('on')) toggle.click();
      return { restored: toggle.classList.contains('on') };
    })()`;
  }

  if (operation !== 'hide-with-background-disabled') {
    throw new Error(`Unsupported hide-button operation: ${operation}`);
  }

  return `(async () => {
    let button=null,toggle=null;
    for(let i=0;i<100;i++){
      button=document.querySelector('#minimizeButton');
      toggle=document.querySelector('#backgroundToggle');
      if(button&&toggle)break;
      await new Promise(resolve=>setTimeout(resolve,100));
    }
    if (!button || !toggle) return { clicked: false, reason: 'missing-control-timeout' };
    if (toggle.classList.contains('on')) toggle.click();
    await new Promise(resolve => setTimeout(resolve, 350));
    const result = { clicked: true, title: button.getAttribute('title'), backgroundEnabled: toggle.classList.contains('on') };
    setTimeout(() => button.click(), 0);
    return result;
  })()`;
}

module.exports = { buildExpression };
