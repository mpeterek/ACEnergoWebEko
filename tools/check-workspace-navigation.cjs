// node tools/check-workspace-navigation.cjs /path/to/HTML-fixtures /path/to/picker-JSON
const {chromium}=require('playwright');
const fs=require('fs');
const path=require('path');
const fixtures=path.resolve(process.argv[2]);
const picker=path.resolve(process.argv[3]);
const assets=path.resolve('src/ACEnergo.Web/wwwroot');
function longTree(html){
 let rows='';
 for(let id=100;id<160;id++)rows+=`<li data-tree-item data-group-id="${id}" data-search-text="${id} Měření"><details><summary><a href="/ElectricityGroup/centres/${id}">Měření ${id}</a></summary><ul><li>Testovací položka A</li><li>Testovací položka B</li></ul></details></li>`;
 rows+='<li data-tree-item data-group-id="999" data-search-text="999 Chyba"><details><summary><a href="/ElectricityGroup/centres/999">Chyba načtení</a></summary></details></li>';
 return html.replace(/(<ul class="measurement-tree"[^>]*>)/,'$1'+rows);
}
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_PATH||'/usr/bin/chromium',args:['--no-sandbox']});
 try{
  for(const viewport of [{width:1440,height:900},{width:390,height:844}]){
   const context=await browser.newContext({viewport,hasTouch:viewport.width<768});
   const page=await context.newPage();
   const errors=[];const posts=[];let documentRequests=0;let slow=false;
   page.on('pageerror',e=>errors.push(e.stack||e.message));
   await page.route('http://navigation.test/**',async route=>{
    const req=route.request(),url=new URL(req.url());
    if(req.method()==='POST'){
     posts.push(req.postData());return route.fulfill({status:200,contentType:'text/html',body:longTree(fs.readFileSync(path.join(fixtures,'configuration.html'),'utf8')).replaceAll('<ul><li>Testovací položka A</li><li>Testovací položka B</li></ul>','<p>Otevřít větev</p>')});
    }
    if(url.searchParams.get('handler')==='Candidates')return route.fulfill({contentType:'application/json',body:fs.readFileSync(path.join(picker,'physical.json'),'utf8')});
    let name=null;
    if(url.pathname==='/ElectricityGroups/centres')name='groups';
    if(url.pathname==='/ElectricityGroup/centres/16')name='configuration';
    if(url.pathname==='/ElectricityGroup/centres/1')name='configuration-one';
    if(url.pathname==='/ElectricityGroupComposition/centres/16')name='composition-sixteen';
    if(url.pathname==='/ElectricityGroupPrices/centres/16')name='type-prices';
    if(url.pathname==='/ElectricityGroupPriceHistory/centres/16')name='type-history';
    if(url.pathname==='/ElectricityGroupPriceYear/centres/16')name='type-year';
    if(name){
     if(req.isNavigationRequest())documentRequests++;
     if(slow&&name==='configuration-one')await new Promise(r=>setTimeout(r,200));
     const fragment=req.headers()['x-workspace-request']==='1'&&(name==='configuration'||name==='composition-sixteen'||name.startsWith('type-'));
     const file=fragment?(name.startsWith('type-')?'fragment-'+name:name==='configuration'?'fragment-configuration':'fragment-composition'):name;
     const body=fs.readFileSync(path.join(fixtures,file+'.html'),'utf8');
     return route.fulfill({contentType:'text/html',body:fragment?body:longTree(body)}).catch(()=>{});
    }
    if(url.pathname.endsWith('/999'))return route.fulfill({status:503,body:'Unavailable test response'});
    let file=path.join(assets,url.pathname);
    if(process.env.ACE_WORKSPACE_MINIFIED==='1'){
     const mapping={'/js/pages/measurement-workspace.js':'measurement-workspace.min.js','/js/pages/composition-picker.js':'composition-picker.min.js','/css/pages/measurement-workspace.css':'measurement-workspace.min.css','/css/pages/group-composition.css':'group-composition.min.css','/css/responsive.css':'responsive.min.css'};
     if(mapping[url.pathname])file=path.join(assets,'generated',mapping[url.pathname]);
    }
    if(!fs.existsSync(file))return route.fulfill({status:404,body:''});
    return route.fulfill({path:file,contentType:file.endsWith('.css')?'text/css':file.endsWith('.js')?'application/javascript':undefined});
   });
   await page.goto('http://navigation.test/ElectricityGroups/centres');
   await page.evaluate(()=>{
    window.__treeNode=document.querySelector('[data-measurement-tree]');window.__documentMarker='retained';
    document.querySelector('[data-group-id="108"] details').open=true;
    document.querySelector('[data-group-id="110"] details').open=true;
   });
   await page.locator('[data-tree-search]').fill(' ');
   const link=page.locator('[data-group-id="16"] summary a');await link.scrollIntoViewIfNeeded();
   let before=await page.locator('[data-measurement-tree]').evaluate(e=>e.scrollTop);
   await link.click();
   await page.waitForFunction(()=>document.querySelector('input[name="GroupName"]')&&document.querySelector('.workspace-content').getAttribute('aria-busy')==='false');
   async function stable(){
    const state=await page.evaluate(()=>({same:window.__treeNode===document.querySelector('[data-measurement-tree]'),marker:window.__documentMarker,top:document.querySelector('[data-measurement-tree]').scrollTop,filter:document.querySelector('[data-tree-search]').value,open:[108,110].every(id=>document.querySelector('[data-group-id="'+id+'"] details').open)}));
    if(!state.same||state.marker!=='retained'||Math.abs(state.top-before)>1||state.filter!==' '||!state.open)throw Error('Tree state lost '+JSON.stringify(state));
   }
   await stable();
   await page.locator('[data-group-id="16"] .tree-components a').scrollIntoViewIfNeeded();
   before=await page.locator('[data-measurement-tree]').evaluate(e=>e.scrollTop);
   await page.locator('[data-group-id="16"] .tree-components a').click();
   await page.waitForSelector('.composition-row');await stable();
   await page.locator('[data-bs-target="#composition-picker"]').click();
   await page.waitForFunction(()=>document.querySelector('[data-picker-rows]').textContent.includes('Test candidate'));
   await page.keyboard.press('Escape');await page.waitForSelector('#composition-picker',{state:'hidden'});
   await page.goBack();await page.waitForSelector('input[name="GroupName"]');await stable();
   await page.goForward();await page.waitForSelector('.composition-row');await stable();
   // Back navigation while a Bootstrap modal is still open must clean up the overlay.
   await page.locator('[data-bs-target="#composition-picker"]').click();
   await page.waitForFunction(()=>document.querySelector('#picker-term')===document.activeElement);
   await page.goBack();await page.waitForSelector('input[name="GroupName"]');
   if(await page.locator('.modal-backdrop').count()||await page.locator('body').evaluate(e=>e.classList.contains('modal-open')))throw Error('Modal leaked after navigation');
   await stable();
   slow=true;
   await page.evaluate(()=>document.querySelector('[data-group-id="1"] summary a').click());
   await page.waitForTimeout(20);
   await page.evaluate(()=>document.querySelector('[data-group-id="16"] summary a').click());
   await page.waitForTimeout(350);await stable();
   if(!await page.locator('h1').textContent().then(text=>text.includes('16')))throw Error('Stale response won race');
   await page.locator('input[name="GroupName"]').fill('Unsaved value');
   await page.evaluate(()=>document.querySelector('[data-group-id="999"] summary a').click());
   await page.waitForFunction(()=>document.querySelector('[data-workspace-status]').getAttribute('role')==='alert');
   if(await page.locator('input[name="GroupName"]').inputValue()!=='Unsaved value')throw Error('Failed request discarded form');
   await stable();
   if(documentRequests!==1)throw Error('Selection reloaded the document');
   const tables=await page.evaluate(()=>window.DataTable.settings.length);
   if(tables!==1)throw Error('Leaked DataTables instances: '+tables);
   // Newly migrated price screens and period selection use the same workspace.
   await page.locator('.workspace-toolbar a[href*="ElectricityGroupPrices"]').click();
   await page.waitForSelector('.type-price-navigation');await stable();
   await page.locator('#type-price-period').fill('2026-12');
   await page.locator('[data-workspace-form] button').click();
   await page.waitForFunction(()=>location.search.includes('Period=2026-12')&&document.querySelector('.workspace-content').getAttribute('aria-busy')==='false');await stable();
   await page.locator('.type-price-navigation a[href*="ElectricityGroupPriceHistory"]').click();
   await page.waitForSelector('table.dataTable');await stable();
   if(!await page.locator('h2').last().textContent().then(t=>t.includes('Historie')))throw Error('History route not rendered');
   await page.locator('.type-price-navigation a[href*="ElectricityGroupPriceYear"]').click();
   await page.waitForSelector('.price-year-table');await stable();
   if(await page.evaluate(()=>DataTable.settings.length)!==1)throw Error('Price table instances leaked');
   await page.goBack();await page.waitForSelector('.type-price-navigation');await stable();
   await page.locator('.workspace-toolbar a[href="/ElectricityGroup/centres/16"]').click();
   await page.waitForSelector('input[name="GroupName"]');await stable();
   await page.locator('input[name="GroupName"]').fill('Unsaved value');
   if(documentRequests!==1)throw Error('Price navigation reloaded the document');
   // Normal POST and redirect remain untouched; session state must restore the tree.
   const beforeOffset=await page.evaluate(()=>document.querySelector('[data-group-id="16"] summary').getBoundingClientRect().top-document.querySelector('[data-measurement-tree]').getBoundingClientRect().top);
   await page.locator('.configuration-actions button').click();
   await page.waitForFunction(()=>window.__documentMarker===undefined&&document.querySelector('input[name="GroupName"]'));
   const restored=await page.evaluate(()=>({offset:document.querySelector('[data-group-id="16"] summary').getBoundingClientRect().top-document.querySelector('[data-measurement-tree]').getBoundingClientRect().top,top:document.querySelector('[data-measurement-tree]').scrollTop,filter:document.querySelector('[data-tree-search]').value,open:[108,110].every(id=>document.querySelector('[data-group-id="'+id+'"] details').open)}));
   if(Math.abs(restored.offset-beforeOffset)>1||restored.filter!==' '||!restored.open)throw Error('POST tree restoration failed '+JSON.stringify(restored));
   const form=new URLSearchParams(posts[0]);
   if(!form.get('__RequestVerificationToken')||!form.get('Version')||form.get('GroupName')!=='Unsaved value')throw Error('POST security fields lost');
   if(errors.length)throw Error(errors.join('\n'));
   console.log(`PASS AJAX workspace ${viewport.width}x${viewport.height}: stable tree, history, modal cleanup, race, error recovery, native POST restore`);
   await context.close();
  }
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
