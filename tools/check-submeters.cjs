// After exporting ACE_RESPONSIVE_DIR fixtures: node tools/check-submeters.cjs /path/to/fixtures
const {chromium}=require('playwright');
const fs=require('fs'),path=require('path');
const fixtures=path.resolve(process.argv[2]),assets=path.resolve('src/ACEnergo.Web/wwwroot');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_PATH||'/usr/bin/chromium',args:['--no-sandbox']});
 try{
  for(const viewport of [{width:1440,height:900},{width:768,height:1024},{width:390,height:844}]){
   const context=await browser.newContext({viewport,hasTouch:viewport.width<1000});const page=await context.newPage();
   let documents=0,failList=false,slowList=false;const errors=[];
   page.on('pageerror',e=>errors.push(e.stack||e.message));
   await page.route('http://submeters.test/**',async route=>{
    const req=route.request(),url=new URL(req.url());
    if(url.pathname==='/ElectricitySubmeters'&&url.searchParams.get('handler')==='Tree'){
     if(failList){failList=false;return route.fulfill({status:503,body:'Unavailable'});}
     if(slowList&&url.searchParams.get('Station')==='2')await new Promise(r=>setTimeout(r,250));
     const json=url.searchParams.get('Search')==='10003'?'submeters-search':url.searchParams.get('Station')==='2'?'submeters-station':url.searchParams.get('InputType')==='EU'?'submeters-type':Number(url.searchParams.get('AfterId'))>0?'submeters-next':'submeters-first';
     return route.fulfill({contentType:'application/json',path:path.join(fixtures,json+'.json')}).catch(()=>{});
    }
    let name=null;
    if(url.pathname==='/ElectricitySubmeters')name='submeters';
    if(url.pathname==='/ElectricitySubmeter/10003')name='submeter';
    if(url.pathname==='/ElectricitySubmeterEvaluation/10003')name='submeter-'+(url.searchParams.get('Mode')||'month');
    if(name){
     if(req.isNavigationRequest())documents++;
     const fragment=req.headers()['x-workspace-request']==='1';
     return route.fulfill({contentType:'text/html',path:path.join(fixtures,(fragment?'fragment-':'')+name+'.html')});
    }
    let file=path.join(assets,url.pathname);
    if(process.env.ACE_SUBMETERS_MINIFIED==='1'){
     const mapping={'/js/pages/measurement-workspace.js':'measurement-workspace.min.js','/js/pages/submeter-tree.js':'submeter-tree.min.js','/css/pages/measurement-workspace.css':'measurement-workspace.min.css','/css/pages/group-composition.css':'group-composition.min.css','/css/pages/submeters.css':'submeters.min.css','/css/responsive.css':'responsive.min.css'};
     if(mapping[url.pathname])file=path.join(assets,'generated',mapping[url.pathname]);
    }
    if(!fs.existsSync(file))return route.fulfill({status:404,body:''});
    return route.fulfill({path:file,contentType:file.endsWith('.js')?'application/javascript':file.endsWith('.css')?'text/css':undefined});
   });
   await page.goto('http://submeters.test/ElectricitySubmeters');
   await page.waitForSelector('[data-group-id="10003"]');
   await page.evaluate(()=>{window.__tree=document.querySelector('[data-measurement-tree]');window.__marker=true;document.querySelector('[data-group-id="10002"] details').open=true;});
   await page.locator('[data-submeter-more]').click();
   await page.waitForSelector('[data-group-id="20003"]');
   if(await page.locator('[data-tree-item]').count()!==131)throw Error('Keyset pagination lost or duplicated rows');
   const link=page.locator('[data-group-id="10003"] summary a');await link.scrollIntoViewIfNeeded();
   const before=await page.locator('[data-measurement-tree]').evaluate(e=>e.scrollTop);
   async function stable(){
    const state=await page.evaluate(()=>({same:window.__tree===document.querySelector('[data-measurement-tree]'),marker:window.__marker,top:document.querySelector('[data-measurement-tree]').scrollTop,open:document.querySelector('[data-group-id="10002"] details').open}));
    if(!state.same||!state.marker||!state.open||Math.abs(state.top-before)>1)throw Error('Submeter tree state lost '+JSON.stringify(state));
   }
   await link.click();await page.waitForSelector('h1');await page.waitForFunction(()=>document.querySelector('h1').textContent.includes('submeter'));await stable();
   if(await page.locator('submeter').count())throw Error('Unsafe database name');
   await page.locator('.workspace-toolbar a[href*="Evaluation"]').click();
   await page.waitForSelector('[data-evaluation-table].dataTable');await stable();
   if(await page.locator('[data-evaluation-table] tbody tr').count()>24)throw Error('Evaluation table not paged');
   const exportHref=await page.locator('[data-native-navigation]').getAttribute('href');
   if(!exportHref.includes('handler=Export')||!exportHref.includes('Date='))throw Error('CSV loses evaluation parameters');
   for(const mode of ['day','year','month']){
    await page.locator('#Mode').selectOption(mode);await page.locator('#Date').fill('2026-10-01');
    await page.locator('[data-workspace-form] button').click();
    await page.waitForFunction(mode=>location.search.includes('Mode='+mode)&&document.querySelector('.workspace-content').getAttribute('aria-busy')==='false',mode);await stable();
    if(await page.evaluate(()=>DataTable.settings.length)!==1)throw Error('Leaked DataTables');
    if(!await page.locator('.consumption-chart svg').isVisible())throw Error('Missing chart');
   }
   await page.goBack();await page.waitForFunction(()=>location.search.includes('Mode=year')&&document.querySelector('.workspace-content').getAttribute('aria-busy')==='false');await stable();
   if(documents!==1)throw Error('Evaluation caused a document reload');
   // Catalogue filtering, retry, stale response cancellation and station address.
   await page.locator('[data-submeter-station]').selectOption('2');await page.waitForFunction(()=>document.querySelectorAll('[data-tree-item]').length===1);
   if(!await page.locator('[data-tree-item] summary').textContent().then(s=>s.includes('0002/0003')))throw Error('Station address wrong');
   failList=true;await page.locator('[data-submeter-station]').selectOption('');
   await page.waitForFunction(()=>document.querySelector('[data-submeter-status]').textContent.includes('nepodařilo'));
   if(!await page.locator('[data-group-id="20003"]').isVisible())throw Error('Failed filter destroyed previous list');
   await page.locator('[data-submeter-more]').click();await page.waitForFunction(()=>document.querySelectorAll('[data-tree-item]').length===100);
   await page.locator('[data-submeter-type]').selectOption('EU');await page.waitForFunction(()=>document.querySelectorAll('[data-tree-item]').length===1&&document.querySelector('[data-group-id="10004"]'));
   await page.locator('[data-submeter-type]').selectOption('');await page.waitForFunction(()=>document.querySelectorAll('[data-tree-item]').length===100);
   slowList=true;await page.locator('[data-submeter-station]').selectOption('2');await page.waitForTimeout(300);
   await page.locator('[data-submeter-station]').selectOption('');await page.locator('[data-tree-search]').fill('10003');
   await page.waitForFunction(()=>document.querySelectorAll('[data-tree-item]').length===1&&document.querySelector('[data-group-id="10003"]'));
   await page.waitForTimeout(350);
   if(await page.locator('[data-tree-item]').count()!==1||!await page.locator('[data-group-id="10003"]').isVisible())throw Error('Stale filter response won');
   if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1))throw Error('Viewport overflow');
   if(errors.length)throw Error(errors.join('\n'));
   console.log(`PASS submeter workspace ${viewport.width}x${viewport.height}: catalogue paging/filters/retry/races, day/month/year AJAX, tree state, chart, DataTables, CSV parameters`);
   await context.close();
  }
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
