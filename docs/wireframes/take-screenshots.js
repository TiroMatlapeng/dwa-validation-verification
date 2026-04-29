const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });

  const baseDir = __dirname;
  const ssDir = path.join(baseDir, 'screenshots-v2');

  // --- Internal wireframes ---
  const internalPages = [
    'dashboard', 'properties', 'register', 'detail',
    'filemaster', 'letters', 'workflow', 'users', 'reports'
  ];

  const internalPage = await context.newPage();
  await internalPage.goto('file://' + path.join(baseDir, 'modern-internal-wireframes.html'));

  // Click the first card to enter the dashboard
  await internalPage.click('.index-card');
  await internalPage.waitForTimeout(500);

  for (const pageId of internalPages) {
    // Use showPage JS function
    await internalPage.evaluate((id) => {
      if (typeof showPage === 'function') showPage(id);
    }, pageId);
    await internalPage.waitForTimeout(300);
    await internalPage.screenshot({
      path: path.join(ssDir, `internal-${pageId}.png`),
      fullPage: false
    });
    console.log(`Captured internal-${pageId}.png`);
  }

  // --- External portal wireframes ---
  const externalPages = [
    'login', 'register', 'dashboard', 'case-detail',
    'correspondence', 'comments', 'documents', 'notifications', 'protest'
  ];

  // Check for alternate IDs used in portal
  const externalAltPages = [
    'login', 'register', 'dashboard', 'case-detail',
    'correspondence', 'comments', 'documents', 'notifications', 'objection'
  ];

  const portalPage = await context.newPage();
  await portalPage.goto('file://' + path.join(baseDir, 'portal-wireframes.html'));
  await portalPage.waitForTimeout(500);

  for (let i = 0; i < externalPages.length; i++) {
    const pageId = externalPages[i];
    const altId = externalAltPages[i];
    // Try clicking the nav button or use showScreen function
    await portalPage.evaluate(({pageId, altId}) => {
      if (typeof showScreen === 'function') {
        showScreen(pageId);
      }
      // Also try clicking buttons with matching text
      const btns = document.querySelectorAll('.wireframe-nav button');
      for (const btn of btns) {
        if (btn.getAttribute('onclick')?.includes(pageId) || btn.getAttribute('onclick')?.includes(altId)) {
          btn.click();
          return;
        }
      }
      // Try direct element visibility
      const el = document.getElementById(pageId) || document.getElementById(altId);
      if (el) {
        document.querySelectorAll('.wireframe').forEach(w => w.classList.remove('active'));
        el.classList.add('active');
      }
    }, {pageId, altId});
    await portalPage.waitForTimeout(300);
    await portalPage.screenshot({
      path: path.join(ssDir, `portal-${pageId}.png`),
      fullPage: false
    });
    console.log(`Captured portal-${pageId}.png`);
  }

  await browser.close();
  console.log('Done!');
})();
