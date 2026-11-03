// Optional Playwright check against rendered editable tariff HTML.
const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] });
    try {
        const page = await browser.newPage();
        await page.goto(process.argv[2] || 'http://127.0.0.1:5099/tariff-cycle-test.html');
        await page.waitForSelector('.tariff-cycle');
        if (await page.locator('.tariff-cycle').count() !== 168) throw Error('Missing editable cells');
        const button = page.locator('.tariff-cycle').first();
        const field = page.locator('select[name="Cells[0]"]');
        const initial = Number(await field.inputValue());
        for (let step = 1; step <= 4; step++) {
            await button.click();
            const expected = String((initial + step) % 3);
            if (await field.inputValue() !== expected) throw Error('Click cycle mismatch');
            if (!await button.evaluate((el, value) => el.classList.contains(`tariff-value-${value}`), expected)) throw Error('Color mismatch');
        }
        for (const key of ['Space', 'Enter']) await button.press(key);
        if (await field.inputValue() !== String(initial)) throw Error('Keyboard cycle mismatch');
        const result = await page.evaluate(() => {
            const values = new FormData(document.querySelector('[data-tariff-editor]'));
            return { count: [...values.keys()].filter(key => key.startsWith('Cells[')).length, dirty: !document.querySelector('[data-unsaved-notice]').hidden };
        });
        if (result.count !== 168 || !result.dirty) throw Error('Form values or dirty notice invalid');
        console.log('PASS: tariff click/keyboard cycle, colors and 168 posted values');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
