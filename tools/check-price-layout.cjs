// Optional browser regression check. Requires Playwright and Chromium.
// Run against rendered yearly-price HTML with a populated table.
const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch({
        executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium',
        headless: true,
        args: ['--no-sandbox']
    });
    try {
        const page = await browser.newPage();
        for (const width of [1920, 1440, 1280, 768, 390]) {
            await page.setViewportSize({ width, height: 1080 });
            await page.goto(process.argv[2] || 'http://127.0.0.1:5099/price-layout-test.html');
            await page.waitForSelector('.dt-search input');
            await page.locator('.dt-search input').focus();
            const result = await page.evaluate(() => {
                const table = document.querySelector('.price-year-table');
                const input = document.querySelector('.dt-search input').getBoundingClientRect();
                const offsets = Array.from(table.querySelectorAll('thead th')).slice(2).map((header, index) => {
                    const title = header.querySelector('a').getBoundingClientRect();
                    const range = document.createRange();
                    range.selectNodeContents(table.tBodies[0].rows[0].cells[index + 2]);
                    return Math.abs(title.right - range.getBoundingClientRect().right);
                });
                return { offset: Math.max(...offsets), inputRight: input.right, pageWidth: document.documentElement.scrollWidth };
            });
            if (result.offset > 2) throw new Error(`Month alignment at ${width}px`);
            if (result.inputRight + 6 > width) throw new Error(`Focus outline clipped at ${width}px`);
            if (result.pageWidth > width + 1) throw new Error(`Page overflow at ${width}px`);
            console.log(`PASS ${width}px`, result);
        }
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
