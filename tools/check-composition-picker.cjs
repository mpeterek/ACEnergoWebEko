// Requires Playwright. HTML and JSON fixtures are exported by ACE_COMPOSITION_HTML / ACE_PICKER_DIR.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const html = path.resolve(process.argv[2]);
const dataDirectory = path.resolve(process.argv[3]);
const assets = path.resolve('src/ACEnergo.Web/wwwroot');
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] });
    try {
        for (const viewport of [{width:1440,height:900},{width:768,height:1024},{width:390,height:844},{width:844,height:390}]) {
            const page = await browser.newPage({ viewport, hasTouch: viewport.width < 1000 });
            const errors = [];
            const posts = [];
            let fail = false;
            page.on('pageerror', error => errors.push(error.message));
            await page.route('http://picker.test/**', async route => {
                const request = route.request();
                const url = new URL(request.url());
                if (request.method() === 'POST') { posts.push(request.postData()); return route.fulfill({status:200,contentType:'text/html',body:'Saved fixture'}); }
                if (url.searchParams.get('handler') === 'Candidates') {
                    if (fail) return route.fulfill({status:503,contentType:'application/json',body:'{"error":"test failure"}'});
                    const category = url.searchParams.get('category');
                    let data = JSON.parse(fs.readFileSync(path.join(dataDirectory, category + '.json')));
                    const type = url.searchParams.get('inputType');
                    const term = url.searchParams.get('term') || '';
                    const after = Number(url.searchParams.get('afterId') || 0);
                    data.rows = data.rows.filter(row => (!type || row.type === type) && (!term || row.name.toLowerCase().includes(term.toLowerCase())) && row.id > after);
                    // Synthetic >100 continuation case exercises the client's append/cursor behavior.
                    if (term === 'more') { data.rows = [{id:after ? 10005 : 10004,name:'Continuation',type:'E',powerUnit:'kW',energyUnit:'kWh',assigned:false}]; data.hasMore = !after; }
                    return route.fulfill({contentType:'application/json',body:JSON.stringify(data)});
                }
                let file = url.pathname === '/' ? html : path.join(assets, url.pathname);
                if (process.env.ACE_PICKER_MINIFIED === '1') {
                    if (url.pathname === '/js/pages/composition-picker.js') file = path.join(assets,'generated/composition-picker.min.js');
                    if (url.pathname === '/js/pages/measurement-workspace.js') file = path.join(assets,'generated/measurement-workspace.min.js');
                    if (url.pathname === '/css/pages/measurement-workspace.css') file = path.join(assets,'generated/measurement-workspace.min.css');
                    if (url.pathname === '/css/responsive.css') file = path.join(assets,'generated/responsive.min.css');
                    if (url.pathname === '/css/pages/group-composition.css') file = path.join(assets,'generated/group-composition.min.css');
                }
                if (!fs.existsSync(file)) return route.fulfill({status:404,body:''});
                return route.fulfill({path:file,contentType:file.endsWith('.css')?'text/css':file.endsWith('.js')?'application/javascript':file.endsWith('.html')?'text/html':undefined});
            });
            await page.goto('http://picker.test/');
            const trigger = page.locator('[data-bs-target="#composition-picker"]');
            await trigger.click();
            await page.waitForSelector('#composition-picker.show');
            await page.waitForFunction(()=>document.querySelector('[data-picker-rows]').textContent.includes('Assigned test'));
            await page.waitForFunction(()=>document.querySelector('#picker-term')===document.activeElement);
            if (await page.locator('[data-picker-rows] tr').count() !== 2) throw Error('Initial list');
            if (await page.locator('[data-picker-rows] tr').first().locator('button').count()) throw Error('Assigned item selectable');
            await page.locator('#picker-type').selectOption('E');
            await page.waitForFunction(()=>document.querySelector('#picker-results').getAttribute('aria-busy')==='false'&&document.querySelectorAll('[data-picker-rows] tr').length===1&&document.querySelector('[data-picker-rows]').textContent.includes('Test candidate'));
            await page.locator('#picker-type').selectOption('');
            await page.locator('#picker-fictitious-tab').click();
            await page.waitForFunction(()=>document.querySelector('[data-picker-rows]').textContent.includes('50000001'));
            if (await page.locator('#picker-results').getAttribute('aria-labelledby') !== 'picker-fictitious-tab') throw Error('ARIA tab relationship');
            await page.locator('#picker-fictitious-tab').press('ArrowLeft');
            await page.waitForFunction(()=>document.querySelectorAll('[data-picker-rows] tr').length===2);
            await page.locator('#picker-term').fill('missing');
            await page.locator('[data-picker-search] button').click();
            await page.waitForFunction(()=>document.querySelector('[data-picker-status]').textContent.includes('Žádné'));
            fail = true;
            await page.locator('[data-picker-search] button').click();
            await page.waitForFunction(()=>document.querySelector('[data-picker-status]').textContent.includes('nepodařilo'));
            fail = false;
            await page.locator('#picker-term').fill('more');
            await page.locator('[data-picker-search] button').click();
            await page.waitForSelector('[data-picker-more]:visible');
            await page.locator('[data-picker-more]').click();
            await page.waitForFunction(()=>document.querySelectorAll('[data-picker-rows] tr').length===2);
            const layout = await page.evaluate(()=>{
                const dialog=document.querySelector('#composition-picker .modal-content').getBoundingClientRect();
                return {pageOverflow:document.documentElement.scrollWidth>innerWidth+1,left:dialog.left,right:dialog.right,top:dialog.top,bottom:dialog.bottom};
            });
            if (layout.pageOverflow || layout.left < -1 || layout.right > viewport.width+1 || layout.top < -1 || layout.bottom > viewport.height+1) throw Error('Modal exceeds viewport '+JSON.stringify(layout));
            await page.keyboard.press('Escape');
            await page.waitForSelector('#composition-picker', {state:'hidden'});
            await page.waitForFunction(()=>document.querySelector('[data-bs-target="#composition-picker"]')===document.activeElement);
            await trigger.click();
            await page.waitForFunction(()=>document.querySelector('[data-picker-results]') || document.querySelector('[data-picker-status]').textContent.indexOf('Načítám')<0);
            await page.locator('#picker-term').fill('Test candidate');
            await page.locator('[data-picker-search] button').click();
            await page.waitForSelector('[data-picker-rows] button[value="10003"]');
            await page.locator('[data-picker-rows] button[value="10003"]').click();
            await page.waitForURL(url=>url.searchParams.get('handler')==='Add');
            const fields = new URLSearchParams(posts[0]);
            if(fields.get('ChildId')!=='10003'||!fields.get('Version')||!fields.get('__RequestVerificationToken'))throw Error('Selected POST values missing');
            if(errors.length)throw Error(errors.join('\n'));
            console.log(`PASS modal ${viewport.width}x${viewport.height}: tabs, type/search, continuation, error/retry, keyboard/focus, POST`);
            await page.close();
        }
    } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
