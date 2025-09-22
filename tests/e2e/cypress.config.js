const { defineConfig } = require('cypress');

module.exports = defineConfig({
  e2e: {
    // Base URL for the application
    baseUrl: process.env.CYPRESS_BASE_URL || 'http://localhost:3000',

    // Test files pattern
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',

    // Support file
    supportFile: 'cypress/support/e2e.js',

    // Video and screenshot settings
    video: true,
    videoCompression: 32,
    screenshotOnRunFailure: true,
    screenshotsFolder: 'cypress/screenshots',
    videosFolder: 'cypress/videos',

    // Viewport settings
    viewportWidth: 1280,
    viewportHeight: 720,

    // Timeouts
    defaultCommandTimeout: 10000,
    pageLoadTimeout: 30000,
    requestTimeout: 15000,
    responseTimeout: 15000,

    // Test retries
    retries: {
      runMode: 2,
      openMode: 0
    },

    // Environment variables
    env: {
      // API endpoints
      ORDER_SERVICE_URL: process.env.ORDER_SERVICE_URL || 'http://localhost:5000',
      USER_SERVICE_URL: process.env.USER_SERVICE_URL || 'http://localhost:5001',
      CATALOG_SERVICE_URL: process.env.CATALOG_SERVICE_URL || 'http://localhost:5002',
      PAYMENT_SERVICE_URL: process.env.PAYMENT_SERVICE_URL || 'http://localhost:5003',
      NOTIFICATION_SERVICE_URL: process.env.NOTIFICATION_SERVICE_URL || 'http://localhost:5004',

      // Test user credentials
      TEST_USER_EMAIL: 'test@smartcommerce.com',
      TEST_USER_PASSWORD: 'TestPassword123!',

      // Feature flags for testing
      ENABLE_FRAUD_DETECTION: true,
      ENABLE_RECOMMENDATIONS: true,
      ENABLE_SEARCH: true,

      // Test data settings
      CLEANUP_TEST_DATA: true,
      USE_MOCK_PAYMENT: true,
      MOCK_EXTERNAL_SERVICES: false
    },

    setupNodeEvents(on, config) {
      // Task definitions for custom commands
      on('task', {
        // Database cleanup task
        cleanupTestData() {
          // Implementation would go here
          return null;
        },

        // Create test products
        createTestProducts(products) {
          // Implementation would create products via API
          console.log('Creating test products:', products.length);
          return null;
        },

        // Create test user
        createTestUser(user) {
          // Implementation would create user via API
          console.log('Creating test user:', user.email);
          return null;
        },

        // Generate test order
        generateTestOrder(orderData) {
          // Implementation would generate realistic test order
          console.log('Generating test order for:', orderData.customerId);
          return null;
        }
      });

      // Plugin configurations
      require('@cypress/code-coverage/task')(on, config);

      // Browser configurations
      on('before:browser:launch', (browser = {}, launchOptions) => {
        // Chrome-specific settings
        if (browser.name === 'chrome') {
          launchOptions.args.push('--disable-dev-shm-usage');
          launchOptions.args.push('--disable-gpu');
          launchOptions.args.push('--no-sandbox');
        }

        // Firefox-specific settings
        if (browser.name === 'firefox') {
          launchOptions.preferences['media.navigator.permission.disabled'] = true;
        }

        return launchOptions;
      });

      // Event listeners
      on('after:spec', (spec, results) => {
        // Log test results
        console.log(`Finished running ${spec.relative}`);
        console.log(`Tests: ${results.stats.tests}, Failures: ${results.stats.failures}`);
      });

      return config;
    },
  },

  component: {
    devServer: {
      framework: 'react',
      bundler: 'webpack',
    },
    supportFile: 'cypress/support/component.js',
    specPattern: 'cypress/component/**/*.cy.{js,jsx,ts,tsx}',
  },

  // Global settings
  chromeWebSecurity: false,
  experimentalStudio: true,
  experimentalWebKitSupport: false,

  // Folders
  fixturesFolder: 'cypress/fixtures',
  downloadsFolder: 'cypress/downloads',

  // Node version
  nodeVersion: 'system',

  // Test isolation
  testIsolation: true,

  // Scrolling behavior
  scrollBehavior: 'center',

  // Animation settings
  animationDistanceThreshold: 5,
  waitForAnimations: true,

  // Element interaction settings
  includeShadowDom: true,
  numTestsKeptInMemory: 50,

  // Experimental features
  experimentalMemoryManagement: true,
});