/**
 * End-to-End Test: Complete Purchase Flow
 *
 * This test covers the complete user journey from product discovery
 * to order completion in the SmartCommerce platform.
 */

describe('Complete Purchase Flow', () => {
  let testUser;
  let testProducts;

  before(() => {
    // Setup test data
    testUser = {
      email: `test-${Date.now()}@example.com`,
      password: 'TestPassword123!',
      firstName: 'Test',
      lastName: 'User',
      address: {
        street: '123 Test Street',
        city: 'Test City',
        state: 'TS',
        zipCode: '12345',
        country: 'US'
      }
    };

    testProducts = [
      {
        name: 'Test Laptop',
        price: 999.99,
        category: 'Electronics',
        description: 'High-performance test laptop for E2E testing'
      },
      {
        name: 'Test Mouse',
        price: 29.99,
        category: 'Electronics',
        description: 'Ergonomic test mouse for E2E testing'
      }
    ];

    // Create test products via API
    testProducts.forEach(product => {
      cy.request('POST', '/api/products', product).then(response => {
        expect(response.status).to.eq(201);
        product.id = response.body.id;
      });
    });
  });

  after(() => {
    // Cleanup test data
    testProducts.forEach(product => {
      if (product.id) {
        cy.request('DELETE', `/api/products/${product.id}`);
      }
    });
  });

  beforeEach(() => {
    // Visit the home page
    cy.visit('/');

    // Set viewport to desktop
    cy.viewport(1280, 720);
  });

  it('should complete a full purchase flow from discovery to order confirmation', () => {
    // Step 1: Product Discovery via Search
    cy.log('Step 1: Product Discovery');

    // Use the search functionality
    cy.get('[data-testid="search-input"]').type('Test Laptop');
    cy.get('[data-testid="search-button"]').click();

    // Verify search results
    cy.get('[data-testid="search-results"]').should('be.visible');
    cy.get('[data-testid="product-card"]').should('have.length.at.least', 1);

    // Verify test laptop appears in results
    cy.contains('[data-testid="product-card"]', 'Test Laptop').should('be.visible');

    // Step 2: Product Detail View
    cy.log('Step 2: Product Detail View');

    // Click on the test laptop
    cy.contains('[data-testid="product-card"]', 'Test Laptop').click();

    // Verify product detail page
    cy.url().should('include', '/products/');
    cy.get('[data-testid="product-title"]').should('contain', 'Test Laptop');
    cy.get('[data-testid="product-price"]').should('contain', '$999.99');
    cy.get('[data-testid="product-description"]').should('be.visible');

    // Verify recommendations section
    cy.get('[data-testid="recommendations"]').should('be.visible');
    cy.get('[data-testid="recommended-products"]').should('exist');

    // Step 3: Add to Cart
    cy.log('Step 3: Add to Cart');

    // Select quantity
    cy.get('[data-testid="quantity-selector"]').select('1');

    // Add to cart
    cy.get('[data-testid="add-to-cart"]').click();

    // Verify cart notification
    cy.get('[data-testid="cart-notification"]').should('be.visible');
    cy.get('[data-testid="cart-notification"]').should('contain', 'Item added to cart');

    // Verify cart count updated
    cy.get('[data-testid="cart-count"]').should('contain', '1');

    // Step 4: Add Second Product
    cy.log('Step 4: Add Second Product');

    // Search for mouse
    cy.get('[data-testid="search-input"]').clear().type('Test Mouse');
    cy.get('[data-testid="search-button"]').click();

    // Add mouse to cart
    cy.contains('[data-testid="product-card"]', 'Test Mouse').click();
    cy.get('[data-testid="add-to-cart"]').click();

    // Verify cart count
    cy.get('[data-testid="cart-count"]').should('contain', '2');

    // Step 5: View Cart
    cy.log('Step 5: View Cart');

    // Open cart
    cy.get('[data-testid="cart-icon"]').click();

    // Verify cart contents
    cy.get('[data-testid="cart-modal"]').should('be.visible');
    cy.get('[data-testid="cart-item"]').should('have.length', 2);

    // Verify products in cart
    cy.get('[data-testid="cart-item"]').should('contain', 'Test Laptop');
    cy.get('[data-testid="cart-item"]').should('contain', 'Test Mouse');

    // Verify total
    cy.get('[data-testid="cart-total"]').should('contain', '$1,029.98');

    // Proceed to checkout
    cy.get('[data-testid="proceed-to-checkout"]').click();

    // Step 6: User Registration/Login
    cy.log('Step 6: User Registration');

    // Should redirect to login/register page
    cy.url().should('include', '/auth');

    // Click register tab
    cy.get('[data-testid="register-tab"]').click();

    // Fill registration form
    cy.get('[data-testid="email-input"]').type(testUser.email);
    cy.get('[data-testid="password-input"]').type(testUser.password);
    cy.get('[data-testid="confirm-password-input"]').type(testUser.password);
    cy.get('[data-testid="first-name-input"]').type(testUser.firstName);
    cy.get('[data-testid="last-name-input"]').type(testUser.lastName);

    // Accept terms
    cy.get('[data-testid="terms-checkbox"]').check();

    // Submit registration
    cy.get('[data-testid="register-button"]').click();

    // Verify registration success
    cy.get('[data-testid="success-message"]').should('be.visible');

    // Should redirect to checkout
    cy.url().should('include', '/checkout');

    // Step 7: Shipping Information
    cy.log('Step 7: Shipping Information');

    // Fill shipping address
    cy.get('[data-testid="street-input"]').type(testUser.address.street);
    cy.get('[data-testid="city-input"]').type(testUser.address.city);
    cy.get('[data-testid="state-select"]').select(testUser.address.state);
    cy.get('[data-testid="zip-input"]').type(testUser.address.zipCode);
    cy.get('[data-testid="country-select"]').select(testUser.address.country);

    // Select shipping method
    cy.get('[data-testid="shipping-method"]').check('standard');

    // Continue to payment
    cy.get('[data-testid="continue-to-payment"]').click();

    // Step 8: Payment Information
    cy.log('Step 8: Payment Information');

    // Fill payment form (using test card numbers)
    cy.get('[data-testid="card-number-input"]').type('4111111111111111');
    cy.get('[data-testid="expiry-input"]').type('12/25');
    cy.get('[data-testid="cvv-input"]').type('123');
    cy.get('[data-testid="cardholder-name-input"]').type(`${testUser.firstName} ${testUser.lastName}`);

    // Fill billing address (same as shipping)
    cy.get('[data-testid="billing-same-as-shipping"]').check();

    // Step 9: Order Review
    cy.log('Step 9: Order Review');

    // Continue to review
    cy.get('[data-testid="continue-to-review"]').click();

    // Verify order summary
    cy.get('[data-testid="order-summary"]').should('be.visible');
    cy.get('[data-testid="order-item"]').should('have.length', 2);

    // Verify totals
    cy.get('[data-testid="subtotal"]').should('contain', '$1,029.98');
    cy.get('[data-testid="shipping-cost"]').should('contain', '$9.99');
    cy.get('[data-testid="tax"]').should('contain', '$83.20');
    cy.get('[data-testid="total"]').should('contain', '$1,123.17');

    // Verify shipping address
    cy.get('[data-testid="shipping-address"]').should('contain', testUser.address.street);

    // Verify payment method
    cy.get('[data-testid="payment-method"]').should('contain', '**** 1111');

    // Step 10: Fraud Detection Check (Background)
    cy.log('Step 10: Place Order with Fraud Detection');

    // Place order
    cy.get('[data-testid="place-order"]').click();

    // Show loading state
    cy.get('[data-testid="processing-order"]').should('be.visible');

    // Wait for fraud detection and payment processing
    cy.get('[data-testid="processing-order"]', { timeout: 10000 }).should('not.exist');

    // Step 11: Order Confirmation
    cy.log('Step 11: Order Confirmation');

    // Should redirect to confirmation page
    cy.url().should('include', '/order-confirmation');

    // Verify order confirmation
    cy.get('[data-testid="order-confirmation"]').should('be.visible');
    cy.get('[data-testid="order-number"]').should('be.visible');
    cy.get('[data-testid="confirmation-message"]').should('contain', 'Thank you for your order');

    // Store order number for later verification
    cy.get('[data-testid="order-number"]').then($orderNumber => {
      const orderNumber = $orderNumber.text();
      cy.wrap(orderNumber).as('orderNumber');
    });

    // Verify order details
    cy.get('[data-testid="confirmed-items"]').should('have.length', 2);
    cy.get('[data-testid="confirmed-total"]').should('contain', '$1,123.17');

    // Verify email notification message
    cy.get('[data-testid="email-notification"]').should('contain', 'confirmation email');

    // Step 12: Verify Order in Account
    cy.log('Step 12: Verify Order in Account');

    // Navigate to account
    cy.get('[data-testid="account-menu"]').click();
    cy.get('[data-testid="my-orders"]').click();

    // Should be on orders page
    cy.url().should('include', '/account/orders');

    // Verify order appears in order history
    cy.get('@orderNumber').then(orderNumber => {
      cy.get('[data-testid="order-list"]').should('contain', orderNumber);
    });

    // Click on the order to view details
    cy.get('[data-testid="order-row"]').first().click();

    // Verify order details page
    cy.get('[data-testid="order-details"]').should('be.visible');
    cy.get('[data-testid="order-status"]').should('contain', 'Processing');

    // Step 13: Verify Recommendations Updated
    cy.log('Step 13: Verify Recommendations');

    // Navigate back to home
    cy.visit('/');

    // Check if recommendations section shows related products
    cy.get('[data-testid="recommendations"]').should('be.visible');
    cy.get('[data-testid="recommended-products"]').should('exist');

    // Recommendations should include electronics category due to purchase history
    cy.get('[data-testid="recommended-products"]')
      .find('[data-testid="product-card"]')
      .should('have.length.at.least', 1);
  });

  it('should handle payment failure gracefully', () => {
    cy.log('Testing payment failure scenario');

    // Add product to cart
    cy.get('[data-testid="search-input"]').type('Test Laptop');
    cy.get('[data-testid="search-button"]').click();
    cy.contains('[data-testid="product-card"]', 'Test Laptop').click();
    cy.get('[data-testid="add-to-cart"]').click();

    // Proceed to checkout (assume user is logged in)
    cy.get('[data-testid="cart-icon"]').click();
    cy.get('[data-testid="proceed-to-checkout"]').click();

    // Skip to payment (assume shipping info is filled)
    cy.get('[data-testid="continue-to-payment"]').click();

    // Use a test card number that will be declined
    cy.get('[data-testid="card-number-input"]').type('4000000000000002');
    cy.get('[data-testid="expiry-input"]').type('12/25');
    cy.get('[data-testid="cvv-input"]').type('123');
    cy.get('[data-testid="cardholder-name-input"]').type('Test User');

    // Continue to review and place order
    cy.get('[data-testid="continue-to-review"]').click();
    cy.get('[data-testid="place-order"]').click();

    // Verify payment failure is handled
    cy.get('[data-testid="error-message"]').should('be.visible');
    cy.get('[data-testid="error-message"]').should('contain', 'payment failed');

    // Verify user can retry payment
    cy.get('[data-testid="retry-payment"]').should('be.visible');
    cy.get('[data-testid="edit-payment"]').should('be.visible');
  });

  it('should handle out-of-stock scenarios', () => {
    cy.log('Testing out-of-stock scenario');

    // Create a product with zero stock via API
    const outOfStockProduct = {
      name: 'Out of Stock Product',
      price: 99.99,
      category: 'Electronics',
      stock: 0
    };

    cy.request('POST', '/api/products', outOfStockProduct).then(response => {
      outOfStockProduct.id = response.body.id;

      // Visit product page directly
      cy.visit(`/products/${outOfStockProduct.id}`);

      // Verify out-of-stock messaging
      cy.get('[data-testid="stock-status"]').should('contain', 'Out of Stock');
      cy.get('[data-testid="add-to-cart"]').should('be.disabled');

      // Verify notify me option
      cy.get('[data-testid="notify-when-available"]').should('be.visible');

      // Cleanup
      cy.request('DELETE', `/api/products/${outOfStockProduct.id}`);
    });
  });

  it('should handle high-risk fraud detection', () => {
    cy.log('Testing fraud detection scenario');

    // Create an order that would trigger fraud detection
    // (using patterns that the ML model identifies as suspicious)

    // Add expensive items to cart
    cy.get('[data-testid="search-input"]').type('Test Laptop');
    cy.get('[data-testid="search-button"]').click();
    cy.contains('[data-testid="product-card"]', 'Test Laptop').click();

    // Add multiple expensive items
    cy.get('[data-testid="quantity-selector"]').select('5');
    cy.get('[data-testid="add-to-cart"]').click();

    // Proceed to checkout with suspicious patterns
    cy.get('[data-testid="cart-icon"]').click();
    cy.get('[data-testid="proceed-to-checkout"]').click();

    // Use shipping address that differs significantly from billing
    cy.get('[data-testid="street-input"]').type('999 Suspicious Ave');
    cy.get('[data-testid="city-input"]').type('Fraud City');
    cy.get('[data-testid="continue-to-payment"]').click();

    // Use a different billing address
    cy.get('[data-testid="billing-same-as-shipping"]').uncheck();
    cy.get('[data-testid="billing-street-input"]').type('123 Different St');
    cy.get('[data-testid="billing-city-input"]').type('Other City');

    // Attempt to place order
    cy.get('[data-testid="continue-to-review"]').click();
    cy.get('[data-testid="place-order"]').click();

    // Verify fraud detection message
    cy.get('[data-testid="fraud-review-message"]', { timeout: 10000 })
      .should('be.visible');
    cy.get('[data-testid="fraud-review-message"]')
      .should('contain', 'additional verification');

    // Verify order is pending review
    cy.get('[data-testid="order-status"]').should('contain', 'Under Review');
  });
});