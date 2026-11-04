// Run from repository root after exporting test HTML with ACE_RESPONSIVE_DIR.
// node tools/check-responsive-layout.cjs /path/to/fixtures
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const fixtures = path.resolve(process.argv[2]);
const assets = path.resolve('src/ACEnergo.Web/wwwroot');
const profiles = [
    { width: 1920, height: 1080 }, { width: 1440, height: 900 },
    { width: 1366, height: 600 }, { width: 1024, height: 768, touch: true },
    { width: 768, height: 1024, touch: true }, { width: 844, height: 390, touch: true },
    { width: 390, height: 844, touch: true }, { width: 320, height: 568, touch: true }
];
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] });
    try {
        for (const profile of profiles) {
            const context = await browser.newContext({ viewport: { width: profile.width, height: profile.height }, hasTouch: !!profile.touch });
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            await page.route('http://responsive.test/**', async route => {
                const name = new URL(route.request().url()).pathname;
                let file = name.endsWith('.html') ? path.join(fixtures, path.basename(name)) : path.join(assets, name);
                if (process.env.ACE_RESPONSIVE_MINIFIED === '1' && name === '/css/responsive.css') file = path.join(assets, 'generated/responsive.min.css');
                if (!fs.existsSync(file)) return route.fulfill({ status: 404, body: '' });
                await route.fulfill({ path: file, contentType: file.endsWith('.css') ? 'text/css' : file.endsWith('.js') ? 'application/javascript' : file.endsWith('.html') ? 'text/html' : undefined });
            });
            for (const name of ['home', 'login', 'meter', 'groups', 'configuration', 'composition', 'prices', 'tariffs']) {
                await page.goto('http://responsive.test/' + name + '.html');
                await page.evaluate(() => document.fonts.ready);
                const measurements = await page.evaluate(() => {
                    const input = document.querySelector('input:not([type=hidden]):not([type=checkbox])');
                    const button = document.querySelector('.workspace-toolbar .btn, .button');
                    const title = document.querySelector('h1');
                    const sidebar = document.querySelector('.measurement-workspace aside');
                    const content = document.querySelector('.workspace-content');
                    return {
                        overflow: document.documentElement.scrollWidth > innerWidth + 1,
                        body: parseFloat(getComputedStyle(document.body).fontSize),
                        input: input ? parseFloat(getComputedStyle(input).fontSize) : 16,
                        control: button ? button.getBoundingClientRect().height : 44,
                        title: title ? parseFloat(getComputedStyle(title).fontSize) : 20,
                        sideBySide: sidebar && content ? sidebar.getBoundingClientRect().right <= content.getBoundingClientRect().left + 1 : null
                    };
                });
                if (measurements.overflow) throw Error(`${name} overflows at ${profile.width}x${profile.height}`);
                if (measurements.body < 14 || measurements.body > 15) throw Error('Unexpected body size');
                if (measurements.title < 20 || measurements.title > 29) throw Error('Unexpected heading size');
                if (profile.touch && measurements.input < 16) throw Error('Touch input is too small: ' + name);
                if (profile.touch && measurements.control < 43.5) throw Error('Touch target is too short: ' + name);
                if (measurements.sideBySide !== null) {
                    const expected = profile.width >= 768 && !(profile.width <= 1023 && profile.height <= 500);
                    if (measurements.sideBySide !== expected) throw Error('Wrong workspace placement: ' + name);
                }
                if (name === 'login') {
                    const toggle = page.locator('.password-toggle');
                    if (await toggle.isVisible()) {
                        const input = page.locator('#Password');
                        await input.fill('test-value'); await toggle.click();
                        if (await input.getAttribute('type') !== 'text' || await input.inputValue() !== 'test-value') throw Error('Password toggle regression');
                    }
                }
            }
            if (errors.length) throw Error(errors.join('\n'));
            console.log(`PASS 8 pages at ${profile.width}x${profile.height}, ${profile.touch ? 'touch' : 'mouse'}`);
            await context.close();
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
