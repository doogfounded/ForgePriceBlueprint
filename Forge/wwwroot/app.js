// Forge Price Blueprint Studio App Logic

// 1. Current State
let blueprintState = {
    BlueprintName: "Enterprise Custom Contract",
    Rules: []
};

// Keep track of collapsed rule indices in the designer view
let collapsedRules = new Set();

// Simulator context variables values
let contextValues = {
    base_price: 250,
    quantity: 120,
    is_partner: false,
    region: "EU"
};

// Preset Scenario Templates definitions
const PRESET_SCENARIOS = {
    enterprise: {
        BlueprintName: "Enterprise Custom Contract",
        Rules: [
            {
                Type: "BasePrice",
                Name: "Base Configuration Rate",
                DefaultPrice: 250.0,
                ContextKey: "base_price"
            },
            {
                Type: "TieredPricing",
                Name: "Volume License Tiers",
                QuantityKey: "quantity",
                Tiers: [
                    { MinQuantity: 10, DiscountPercentage: 0.05 },
                    { MinQuantity: 50, DiscountPercentage: 0.10 },
                    { MinQuantity: 100, DiscountPercentage: 0.15 },
                    { MinQuantity: 500, DiscountPercentage: 0.25 }
                ]
            },
            {
                Type: "PercentageAdjustment",
                Name: "Partner Discount",
                Factor: 0.90,
                ConditionKey: "is_partner",
                Description: "Partner Channel 10% discount"
            },
            {
                Type: "FlatAdjustment",
                Name: "Shipping & Handling Surcharge",
                Amount: 15.0,
                ConditionKey: "quantity < 10",
                Description: "Flat rate standard shipping fee for small orders"
            },
            {
                Type: "PercentageAdjustment",
                Name: "International Tax Surcharge",
                Factor: 1.15,
                ConditionKey: "region != US",
                Description: "International regional sales tax of 15%"
            },
            {
                Type: "PriceCap",
                Name: "Contract Price Floor Cap",
                MinPrice: 130.0,
                MaxPrice: null
            }
        ]
    },
    saas: {
        BlueprintName: "SaaS Tiered Subscription",
        Rules: [
            {
                Type: "BasePrice",
                Name: "SaaS Base Subscription",
                DefaultPrice: 50.0,
                ContextKey: "plan_base_price"
            },
            {
                Type: "TieredPricing",
                Name: "User Seats Tiered Pricing",
                QuantityKey: "seats",
                Tiers: [
                    { MinQuantity: 5, DiscountPercentage: 0.05 },
                    { MinQuantity: 10, DiscountPercentage: 0.10 },
                    { MinQuantity: 25, DiscountPercentage: 0.20 },
                    { MinQuantity: 100, DiscountPercentage: 0.35 }
                ]
            },
            {
                Type: "FlatAdjustment",
                Name: "Dedicated Support Add-on",
                Amount: 150.0,
                ConditionKey: "is_enterprise",
                Description: "Dedicated enterprise support engineer SLA surcharge"
            },
            {
                Type: "PercentageAdjustment",
                Name: "Annual Billing Discount",
                Factor: 0.833,
                ConditionKey: "billing_interval == annual",
                Description: "Get 2 months free with annual subscription"
            },
            {
                Type: "PriceCap",
                Name: "Subscription Price Floor",
                MinPrice: 30.0,
                MaxPrice: null
            }
        ]
    },
    ecommerce: {
        BlueprintName: "E-commerce Holiday Promo",
        Rules: [
            {
                Type: "BasePrice",
                Name: "Cart Checkout Price",
                DefaultPrice: 100.0,
                ContextKey: "cart_total"
            },
            {
                Type: "PercentageAdjustment",
                Name: "Holiday Coupon Promo",
                Factor: 0.75,
                ConditionKey: "coupon_code == HOLIDAY25",
                Description: "Apply 25% Christmas coupon discount"
            },
            {
                Type: "PercentageAdjustment",
                Name: "VIP Customer Discount",
                Factor: 0.95,
                ConditionKey: "is_vip",
                Description: "Additional 5% discount for loyalty members"
            },
            {
                Type: "FlatAdjustment",
                Name: "Free Shipping Discount",
                Amount: -10.0,
                ConditionKey: "cart_total >= 50",
                Description: "Waive shipping charge for orders over $50"
            },
            {
                Type: "FlatAdjustment",
                Name: "Standard Shipping Surcharge",
                Amount: 10.0,
                ConditionKey: "cart_total < 50",
                Description: "Add standard shipping and handling fee"
            }
        ]
    }
};

// Default rule structures when adding a new one
const DEFAULT_RULES = {
    BasePrice: () => ({
        Type: "BasePrice",
        Name: "Base Configuration Rate",
        DefaultPrice: 250.0,
        ContextKey: "base_price"
    }),
    TieredPricing: () => ({
        Type: "TieredPricing",
        Name: "Volume License Tiers",
        QuantityKey: "quantity",
        Tiers: [
            { MinQuantity: 10, DiscountPercentage: 0.05 },
            { MinQuantity: 50, DiscountPercentage: 0.10 },
            { MinQuantity: 100, DiscountPercentage: 0.15 },
            { MinQuantity: 500, DiscountPercentage: 0.25 }
        ]
    }),
    PercentageAdjustment: () => ({
        Type: "PercentageAdjustment",
        Name: "Partner Discount",
        Factor: 0.90,
        ConditionKey: "is_partner",
        Description: "Partner Channel 10% discount"
    }),
    FlatAdjustment: () => ({
        Type: "FlatAdjustment",
        Name: "Shipping Surcharge",
        Amount: 15.0,
        ConditionKey: "quantity < 10",
        Description: "Flat rate standard shipping fee for small orders"
    }),
    PriceCap: () => ({
        Type: "PriceCap",
        Name: "Contract Price Floor Cap",
        MinPrice: 130.0,
        MaxPrice: null
    })
};

// 2. DOM Elements
const rulesContainer = document.getElementById('rules-container');
const btnAddRuleToggle = document.getElementById('btn-add-rule-toggle');
const ruleTypeDropdown = document.getElementById('rule-type-dropdown');
const blueprintNameInput = document.getElementById('blueprint-name');
const contextInputsContainer = document.getElementById('context-inputs-container');
const traceContainer = document.getElementById('trace-container');
const finalPriceDisplay = document.getElementById('final-price-value');
const statInitialPrice = document.getElementById('stat-initial-price');
const statTotalAdjustment = document.getElementById('stat-total-adjustment');
const statSavingsPct = document.getElementById('stat-savings-pct');
const validationBar = document.getElementById('validation-status');
const validationMessage = document.getElementById('validation-message');
const iconValid = validationBar.querySelector('.icon-valid');
const iconInvalid = validationBar.querySelector('.icon-invalid');
const jsonEditor = document.getElementById('json-editor');
const yamlEditor = document.getElementById('yaml-editor');
const csharpOutput = document.getElementById('csharp-output');
const cppOutput = document.getElementById('cpp-output');
const btnSave = document.getElementById('btn-save');
const toast = document.getElementById('toast');
const btnResetContext = document.getElementById('btn-reset-context');
const presetSelector = document.getElementById('preset-selector');

// 3. Event Listeners Initialization
function initEvents() {
    // Dropdown toggle
    btnAddRuleToggle.addEventListener('click', (e) => {
        e.stopPropagation();
        ruleTypeDropdown.classList.toggle('hidden');
    });

    document.addEventListener('click', () => {
        ruleTypeDropdown.classList.add('hidden');
    });

    // Add rule items
    ruleTypeDropdown.querySelectorAll('.dropdown-item').forEach(item => {
        item.addEventListener('click', (e) => {
            const type = item.getAttribute('data-type');
            addRule(type);
        });
    });

    // Blueprint Name change
    blueprintNameInput.addEventListener('input', () => {
        blueprintState.BlueprintName = blueprintNameInput.value;
        onVisualChange();
    });

    // Preset Scenario selection
    presetSelector.addEventListener('change', () => {
        const selected = presetSelector.value;
        if (PRESET_SCENARIOS[selected]) {
            blueprintState = JSON.parse(JSON.stringify(PRESET_SCENARIOS[selected]));
            collapsedRules.clear();
            
            // Adjust context simulator inputs to match the preset variables naturally
            if (selected === 'saas') {
                contextValues = {
                    plan_base_price: 50.0,
                    seats: 25,
                    is_enterprise: false,
                    billing_interval: "annual"
                };
            } else if (selected === 'ecommerce') {
                contextValues = {
                    cart_total: 100.0,
                    coupon_code: "HOLIDAY25",
                    is_vip: true
                };
            } else {
                contextValues = {
                    base_price: 250,
                    quantity: 120,
                    is_partner: false,
                    region: "EU"
                };
            }
            
            onVisualChange();
            renderContextInputs();
            runSimulator();
        }
    });

    // Save Button
    btnSave.addEventListener('click', saveBlueprintToDisk);

    // Code editors keyup / change
    jsonEditor.addEventListener('input', onJSONEditorInput);
    yamlEditor.addEventListener('input', onYAMLEditorInput);

    // Reset Context
    btnResetContext.addEventListener('click', () => {
        contextValues = {
            base_price: 250,
            quantity: 120,
            is_partner: false,
            region: "EU"
        };
        renderContextInputs();
        runSimulator();
    });

    // Tab buttons
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
            
            btn.classList.add('active');
            const tabId = btn.getAttribute('data-tab');
            document.getElementById(tabId).classList.add('active');
        });
    });

    // Copy Buttons
    document.querySelectorAll('.btn-copy').forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            const targetEl = document.getElementById(targetId);
            const textToCopy = targetEl.value || targetEl.textContent;
            
            navigator.clipboard.writeText(textToCopy).then(() => {
                const originalText = btn.textContent;
                btn.textContent = 'Copied!';
                setTimeout(() => btn.textContent = originalText, 1500);
            });
        });
    });
}

// 4. Rule Mutators
function addRule(type) {
    if (DEFAULT_RULES[type]) {
        blueprintState.Rules.push(DEFAULT_RULES[type]());
        onVisualChange();
        // Scroll to bottom of rules
        rulesContainer.scrollTop = rulesContainer.scrollHeight;
    }
}

function deleteRule(index) {
    blueprintState.Rules.splice(index, 1);
    onVisualChange();
}

function moveRule(index, direction) {
    const targetIndex = index + direction;
    if (targetIndex >= 0 && targetIndex < blueprintState.Rules.length) {
        const temp = blueprintState.Rules[index];
        blueprintState.Rules[index] = blueprintState.Rules[targetIndex];
        blueprintState.Rules[targetIndex] = temp;
        onVisualChange();
    }
}

function updateRuleField(index, field, value) {
    const rule = blueprintState.Rules[index];
    if (value === '' || value === undefined) {
        if (field === 'MinPrice' || field === 'MaxPrice') {
            rule[field] = null;
        }
    } else if (!isNaN(value) && value !== '' && field !== 'ContextKey' && field !== 'QuantityKey' && field !== 'ConditionKey' && field !== 'Description' && field !== 'Name') {
        rule[field] = parseFloat(value);
    } else {
        rule[field] = value;
    }
    onVisualChange(false); // Update codes and simulator, don't re-render visual cards to avoid losing cursor focus
}

function updateTierField(ruleIndex, tierIndex, field, value) {
    const rule = blueprintState.Rules[ruleIndex];
    const tier = rule.Tiers[tierIndex];
    if (!isNaN(value) && value !== '') {
        tier[field] = parseFloat(value);
    }
    onVisualChange(false);
}

function addTier(ruleIndex) {
    const rule = blueprintState.Rules[ruleIndex];
    rule.Tiers.push({ MinQuantity: 0, DiscountPercentage: 0.0 });
    onVisualChange();
}

function deleteTier(ruleIndex, tierIndex) {
    const rule = blueprintState.Rules[ruleIndex];
    rule.Tiers.splice(tierIndex, 1);
    onVisualChange();
}

// 5. Visual Renderers
function renderVisualDesigner() {
    rulesContainer.innerHTML = '';
    blueprintNameInput.value = blueprintState.BlueprintName;

    if (blueprintState.Rules.length === 0) {
        rulesContainer.innerHTML = `<div class="glass-card" style="text-align: center; color: var(--text-muted); padding: 2rem;">No rules defined. Click "Add Pricing Rule" below to get started.</div>`;
        return;
    }

    blueprintState.Rules.forEach((rule, idx) => {
        const card = document.createElement('div');
        card.className = 'rule-card glass-card';
        card.setAttribute('data-ruletype', rule.Type);
        
        // Header
        const header = document.createElement('div');
        header.className = 'rule-header';
        
        const titleArea = document.createElement('div');
        titleArea.className = 'rule-title-area';
        
        const nameInput = document.createElement('input');
        nameInput.className = 'rule-name-input';
        nameInput.value = rule.Name || '';
        nameInput.placeholder = 'Rule Name...';
        nameInput.addEventListener('input', (e) => updateRuleField(idx, 'Name', e.target.value));
        
        const typeBadge = document.createElement('span');
        typeBadge.className = 'rule-type-badge';
        typeBadge.textContent = rule.Type;
        
        titleArea.appendChild(nameInput);
        titleArea.appendChild(typeBadge);
        
        // Controls (Collapse, Up, Down, Delete)
        const controls = document.createElement('div');
        controls.className = 'rule-controls';

        const isCollapsed = collapsedRules.has(idx);

        const btnCollapse = document.createElement('button');
        btnCollapse.className = 'btn-ctrl btn-collapse';
        btnCollapse.innerHTML = isCollapsed 
            ? `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 5 15 12 9 19"/></svg>` // chevron right
            : `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>`; // chevron down
        btnCollapse.title = isCollapsed ? "Expand Rule" : "Collapse Rule";
        btnCollapse.addEventListener('click', () => {
            if (collapsedRules.has(idx)) {
                collapsedRules.delete(idx);
            } else {
                collapsedRules.add(idx);
            }
            renderVisualDesigner();
        });
        
        const btnUp = document.createElement('button');
        btnUp.className = 'btn-ctrl';
        btnUp.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="18 15 12 9 6 15"/></svg>`;
        btnUp.title = "Move Up";
        btnUp.disabled = idx === 0;
        btnUp.addEventListener('click', () => moveRule(idx, -1));
        
        const btnDown = document.createElement('button');
        btnDown.className = 'btn-ctrl';
        btnDown.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>`;
        btnDown.title = "Move Down";
        btnDown.disabled = idx === blueprintState.Rules.length - 1;
        btnDown.addEventListener('click', () => moveRule(idx, 1));
        
        const btnDel = document.createElement('button');
        btnDel.className = 'btn-ctrl btn-delete';
        btnDel.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>`;
        btnDel.title = "Delete Rule";
        btnDel.addEventListener('click', () => deleteRule(idx));
        
        controls.appendChild(btnCollapse);
        controls.appendChild(btnUp);
        controls.appendChild(btnDown);
        controls.appendChild(btnDel);
        
        header.appendChild(titleArea);
        header.appendChild(controls);
        card.appendChild(header);

        // Body fields depending on type
        const body = document.createElement('div');
        body.className = 'rule-body';
        if (isCollapsed) {
            card.classList.add('collapsed');
            body.classList.add('hidden');
        }
        
        if (rule.Type === 'BasePrice') {
            body.innerHTML = `
                <div class="field-group">
                    <label>Default Price ($)</label>
                    <input type="number" step="0.01" value="${rule.DefaultPrice}" oninput="updateRuleField(${idx}, 'DefaultPrice', this.value)">
                </div>
                <div class="field-group">
                    <label>Context Key</label>
                    <input type="text" value="${rule.ContextKey || 'base_price'}" oninput="updateRuleField(${idx}, 'ContextKey', this.value)">
                </div>
            `;
        }
        else if (rule.Type === 'PercentageAdjustment') {
            body.innerHTML = `
                <div class="field-group">
                    <label>Factor (e.g. 0.90 = -10%)</label>
                    <input type="number" step="0.01" value="${rule.Factor}" oninput="updateRuleField(${idx}, 'Factor', this.value)">
                </div>
                <div class="field-group">
                    <label>Condition Key / Expr</label>
                    <input type="text" value="${rule.ConditionKey || ''}" placeholder="e.g. is_partner" oninput="updateRuleField(${idx}, 'ConditionKey', this.value)">
                </div>
                <div class="field-group span-2">
                    <label>Description</label>
                    <input type="text" value="${rule.Description || ''}" placeholder="Brief details..." oninput="updateRuleField(${idx}, 'Description', this.value)">
                </div>
            `;
        }
        else if (rule.Type === 'FlatAdjustment') {
            body.innerHTML = `
                <div class="field-group">
                    <label>Amount ($)</label>
                    <input type="number" step="0.01" value="${rule.Amount}" oninput="updateRuleField(${idx}, 'Amount', this.value)">
                </div>
                <div class="field-group">
                    <label>Condition Key / Expr</label>
                    <input type="text" value="${rule.ConditionKey || ''}" placeholder="e.g. quantity < 10" oninput="updateRuleField(${idx}, 'ConditionKey', this.value)">
                </div>
                <div class="field-group span-2">
                    <label>Description</label>
                    <input type="text" value="${rule.Description || ''}" placeholder="Brief details..." oninput="updateRuleField(${idx}, 'Description', this.value)">
                </div>
            `;
        }
        else if (rule.Type === 'PriceCap') {
            body.innerHTML = `
                <div class="field-group">
                    <label>Min Floor Price ($ or empty)</label>
                    <input type="number" step="0.01" value="${rule.MinPrice !== null && rule.MinPrice !== undefined ? rule.MinPrice : ''}" placeholder="None" oninput="updateRuleField(${idx}, 'MinPrice', this.value)">
                </div>
                <div class="field-group">
                    <label>Max Ceiling Price ($ or empty)</label>
                    <input type="number" step="0.01" value="${rule.MaxPrice !== null && rule.MaxPrice !== undefined ? rule.MaxPrice : ''}" placeholder="None" oninput="updateRuleField(${idx}, 'MaxPrice', this.value)">
                </div>
            `;
        }
        else if (rule.Type === 'TieredPricing') {
            const tiersEditor = document.createElement('div');
            tiersEditor.className = 'tiers-editor';
            
            let tiersHtml = `
                <div class="tiers-editor-header">
                    <h4>Quantity Discount Tiers</h4>
                    <button class="btn-text" onclick="addTier(${idx})">+ Add Tier</button>
                </div>
                <div class="tiers-table">
                    <div class="tier-row" style="margin-bottom: 0.2rem; opacity: 0.7;">
                        <span style="font-size: 0.7rem; font-weight: bold;">Min Qty</span>
                        <span style="font-size: 0.7rem; font-weight: bold;">Discount (e.g. 0.10 = 10%)</span>
                        <span></span>
                    </div>
            `;

            (rule.Tiers || []).forEach((tier, tIdx) => {
                tiersHtml += `
                    <div class="tier-row">
                        <input type="number" step="1" value="${tier.MinQuantity}" oninput="updateTierField(${idx}, ${tIdx}, 'MinQuantity', this.value)">
                        <input type="number" step="0.01" value="${tier.DiscountPercentage}" oninput="updateTierField(${idx}, ${tIdx}, 'DiscountPercentage', this.value)">
                        <button class="btn-ctrl btn-delete" onclick="deleteTier(${idx}, ${tIdx})" title="Delete Tier">
                            <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/></svg>
                        </button>
                    </div>
                `;
            });

            tiersHtml += `</div>`;
            
            // Context quantity variable
            tiersHtml += `
                <div class="field-group">
                    <label>Quantity Key</label>
                    <input type="text" value="${rule.QuantityKey || 'quantity'}" oninput="updateRuleField(${idx}, 'QuantityKey', this.value)">
                </div>
            `;

            tiersEditor.innerHTML = tiersHtml;
            body.appendChild(tiersEditor);
        }

        card.appendChild(body);
        rulesContainer.appendChild(card);
    });
}

// Extract variables from rules condition keys or expressions to render dynamically in simulator
function scanContextVariables() {
    const vars = new Set(["base_price", "quantity", "region", "is_partner"]); // defaults

    blueprintState.Rules.forEach(rule => {
        if (rule.ContextKey) vars.add(rule.ContextKey);
        if (rule.QuantityKey) vars.add(rule.QuantityKey);
        if (rule.ConditionKey) {
            // condition might be like "quantity < 10" or "region != US" or just "is_partner"
            // extract the first word which is typically the variable name
            const match = rule.ConditionKey.trim().match(/^([a-zA-Z_][a-zA-Z0-9_]*)/);
            if (match) {
                vars.add(match[1]);
            }
        }
    });

    return Array.from(vars);
}

function renderContextInputs() {
    const vars = scanContextVariables();
    contextInputsContainer.innerHTML = '';

    vars.forEach(vName => {
        const item = document.createElement('div');
        item.className = 'context-item';

        // Infer type
        let isBool = vName.startsWith('is_') || vName === 'is_partner' || typeof contextValues[vName] === 'boolean';
        let isNum = vName === 'quantity' || vName === 'base_price' || vName.includes('price') || vName.includes('qty') || !isNaN(contextValues[vName]);

        if (contextValues[vName] === undefined) {
            contextValues[vName] = isBool ? false : (isNum ? 100 : "US");
        }

        if (isBool) {
            item.className = 'context-item span-2';
            item.innerHTML = `
                <div class="checkbox-wrapper">
                    <input type="checkbox" id="ctx-${vName}" ${contextValues[vName] ? 'checked' : ''}>
                    <label for="ctx-${vName}">${vName} <span style="opacity: 0.5; font-size: 0.75rem;">(boolean)</span></label>
                </div>
            `;
            const checkbox = item.querySelector('input');
            checkbox.addEventListener('change', () => {
                contextValues[vName] = checkbox.checked;
                runSimulator();
            });
        } else if (isNum) {
            item.innerHTML = `
                <label for="ctx-${vName}">${vName} <span style="opacity: 0.5;">(number)</span></label>
                <input type="number" id="ctx-${vName}" value="${contextValues[vName]}">
            `;
            const input = item.querySelector('input');
            input.addEventListener('input', () => {
                contextValues[vName] = parseFloat(input.value) || 0;
                runSimulator();
            });
        } else {
            item.innerHTML = `
                <label for="ctx-${vName}">${vName} <span style="opacity: 0.5;">(string)</span></label>
                <input type="text" id="ctx-${vName}" value="${contextValues[vName]}">
            `;
            const input = item.querySelector('input');
            input.addEventListener('input', () => {
                contextValues[vName] = input.value;
                runSimulator();
            });
        }

        contextInputsContainer.appendChild(item);
    });
}

// 6. Pricing Simulator Engine (JS Implementation)
function evaluateCondition(expr, context) {
    if (!expr || expr.trim() === '') return true;

    // Simple expressions parsing (e.g., "quantity < 10", "region != US", "is_partner")
    const operators = ["==", "!=", ">=", "<=", ">", "<"];
    let op = null;
    let opPos = -1;

    for (const possibleOp of operators) {
        opPos = expr.indexOf(possibleOp);
        if (opPos !== -1) {
            op = possibleOp;
            break;
        }
    }

    if (!op) {
        // Boolean check e.g. "is_partner"
        const key = expr.trim();
        return !!context[key];
    }

    let key = expr.substring(0, opPos).trim();
    let valStr = expr.substring(opPos + op.length).trim();

    if (context[key] === undefined) {
        return false;
    }

    let left = context[key];
    
    // Type match comparison
    if (typeof left === 'number') {
        let right = parseFloat(valStr);
        if (isNaN(right)) return false;
        if (op === "==") return left === right;
        if (op === "!=") return left != right;
        if (op === ">=") return left >= right;
        if (op === "<=") return left <= right;
        if (op === ">") return left > right;
        if (op === "<") return left < right;
    }
    else if (typeof left === 'boolean') {
        let right = (valStr === "true" || valStr === "1");
        if (op === "==") return left === right;
        if (op === "!=") return left != right;
    }
    else if (typeof left === 'string') {
        let right = valStr;
        if (right.startsWith('"') && right.endsWith('"')) {
            right = right.substring(1, right.length - 1);
        }
        if (op === "==") return left === right;
        if (op === "!=") return left != right;
    }

    return false;
}

function runSimulator() {
    let currentPrice = 0.0;
    let initialBase = 0.0;
    const auditTrail = [];

    blueprintState.Rules.forEach(rule => {
        let step = {
            ruleName: rule.Name,
            type: rule.Type,
            active: false,
            inputPrice: currentPrice,
            outputPrice: currentPrice,
            adjustment: 0.0,
            description: ""
        };

        if (rule.Type === 'BasePrice') {
            const key = rule.ContextKey || "base_price";
            const val = (contextValues[key] !== undefined) ? contextValues[key] : rule.DefaultPrice;
            
            initialBase = val;
            currentPrice = val;
            step.active = true;
            step.adjustment = currentPrice - step.inputPrice;
            step.outputPrice = currentPrice;
            step.description = `Initialized base price to $${val.toFixed(2)} from ${contextValues[key] !== undefined ? 'context key "'+key+'"' : 'rule default'}.`;
        }
        else if (rule.Type === 'PercentageAdjustment') {
            const isActive = evaluateCondition(rule.ConditionKey, contextValues);
            if (isActive) {
                currentPrice *= rule.Factor;
                step.active = true;
                step.adjustment = currentPrice - step.inputPrice;
                step.outputPrice = currentPrice;
                step.description = rule.Description || `Applied factor multiplier of ${rule.Factor}x under condition "${rule.ConditionKey}".`;
            } else {
                step.description = `Condition "${rule.ConditionKey || 'None'}" was not met. Rule skipped.`;
            }
        }
        else if (rule.Type === 'FlatAdjustment') {
            const isActive = evaluateCondition(rule.ConditionKey, contextValues);
            if (isActive) {
                currentPrice += rule.Amount;
                step.active = true;
                step.adjustment = rule.Amount;
                step.outputPrice = currentPrice;
                step.description = rule.Description || `Applied flat adjustment of $${rule.Amount.toFixed(2)} under condition "${rule.ConditionKey}".`;
            } else {
                step.description = `Condition "${rule.ConditionKey || 'None'}" was not met. Rule skipped.`;
            }
        }
        else if (rule.Type === 'PriceCap') {
            let adjusted = currentPrice;
            let capped = false;
            let capDetails = [];

            if (rule.MinPrice !== null && rule.MinPrice !== undefined && adjusted < rule.MinPrice) {
                adjusted = rule.MinPrice;
                capped = true;
                capDetails.push(`floor of $${rule.MinPrice.toFixed(2)}`);
            }
            if (rule.MaxPrice !== null && rule.MaxPrice !== undefined && adjusted > rule.MaxPrice) {
                adjusted = rule.MaxPrice;
                capped = true;
                capDetails.push(`ceiling of $${rule.MaxPrice.toFixed(2)}`);
            }

            if (capped) {
                currentPrice = adjusted;
                step.active = true;
                step.adjustment = currentPrice - step.inputPrice;
                step.outputPrice = currentPrice;
                step.description = `Price capped at ${capDetails.join(' and ')}.`;
            } else {
                step.description = `Price ($${currentPrice.toFixed(2)}) is within caps (Min: ${rule.MinPrice != null ? '$'+rule.MinPrice.toFixed(2) : 'None'}, Max: ${rule.MaxPrice != null ? '$'+rule.MaxPrice.toFixed(2) : 'None'}).`;
            }
        }
        else if (rule.Type === 'TieredPricing') {
            const qKey = rule.QuantityKey || "quantity";
            const qty = contextValues[qKey] || 0.0;
            let discount = 0.0;

            // Sort tiers ascending by MinQuantity
            const sortedTiers = [...(rule.Tiers || [])].sort((a, b) => a.MinQuantity - b.MinQuantity);
            
            for (const tier of sortedTiers) {
                if (qty >= tier.MinQuantity) {
                    discount = tier.DiscountPercentage;
                }
            }

            if (discount > 0.0) {
                currentPrice *= (1.0 - discount);
                step.active = true;
                step.adjustment = currentPrice - step.inputPrice;
                step.outputPrice = currentPrice;
                step.description = `Applied volume discount of ${(discount*100).toFixed(0)}% for quantity of ${qty} (Tier Min Qty reached: ${rule.Tiers.find(t => t.DiscountPercentage === discount).MinQuantity}).`;
            } else {
                step.description = `Quantity of ${qty} did not qualify for volume tiers.`;
            }
        }

        auditTrail.push(step);
    });

    renderTrace(auditTrail);

    // Update displays
    finalPriceDisplay.textContent = currentPrice.toFixed(2);
    statInitialPrice.textContent = `$${initialBase.toFixed(2)}`;
    
    const netAdjustment = currentPrice - initialBase;
    statTotalAdjustment.textContent = (netAdjustment >= 0 ? '+' : '') + `$${netAdjustment.toFixed(2)}`;
    if (netAdjustment < 0) {
        statTotalAdjustment.style.color = 'var(--color-active)';
    } else if (netAdjustment > 0) {
        statTotalAdjustment.style.color = 'var(--color-cap)';
    } else {
        statTotalAdjustment.style.color = 'var(--text-secondary)';
    }

    const savingsPct = initialBase > 0 ? (1 - (currentPrice / initialBase)) * 100 : 0;
    statSavingsPct.textContent = savingsPct > 0 ? `${savingsPct.toFixed(0)}% OFF` : `${Math.abs(savingsPct).toFixed(0)}%`;
    if (savingsPct > 0) {
        statSavingsPct.className = 'stat-val discount-pct';
    } else {
        statSavingsPct.className = 'stat-val';
    }
    drawPricingCurve();
}

function simulatePriceForQty(qty, qKey) {
    const originalVal = contextValues[qKey];
    contextValues[qKey] = qty;
    
    let currentPrice = 0.0;
    blueprintState.Rules.forEach(rule => {
        if (rule.Type === 'BasePrice') {
            const key = rule.ContextKey || "base_price";
            currentPrice = (contextValues[key] !== undefined) ? contextValues[key] : rule.DefaultPrice;
        }
        else if (rule.Type === 'PercentageAdjustment') {
            if (evaluateCondition(rule.ConditionKey, contextValues)) {
                currentPrice *= rule.Factor;
            }
        }
        else if (rule.Type === 'FlatAdjustment') {
            if (evaluateCondition(rule.ConditionKey, contextValues)) {
                currentPrice += rule.Amount;
            }
        }
        else if (rule.Type === 'PriceCap') {
            if (rule.MinPrice != null && currentPrice < rule.MinPrice) {
                currentPrice = rule.MinPrice;
            }
            if (rule.MaxPrice != null && currentPrice > rule.MaxPrice) {
                currentPrice = rule.MaxPrice;
            }
        }
        else if (rule.Type === 'TieredPricing') {
            const ruleQKey = rule.QuantityKey || "quantity";
            const ruleQty = contextValues[ruleQKey] || 0.0;
            let discount = 0.0;
            const sortedTiers = [...(rule.Tiers || [])].sort((a, b) => a.MinQuantity - b.MinQuantity);
            for (const tier of sortedTiers) {
                if (ruleQty >= tier.MinQuantity) {
                    discount = tier.DiscountPercentage;
                }
            }
            if (discount > 0.0) {
                currentPrice *= (1.0 - discount);
            }
        }
    });
    
    contextValues[qKey] = originalVal;
    return currentPrice;
}

function drawPricingCurve() {
    const canvas = document.getElementById('pricing-curve-canvas');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    
    // Resize buffer if CSS dimensions changed
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx.scale(dpr, dpr);
    
    const width = rect.width;
    const height = rect.height;
    ctx.clearRect(0, 0, width, height);

    // 1. Identify active quantity key
    let qKey = "quantity";
    blueprintState.Rules.forEach(rule => {
        if (rule.Type === 'TieredPricing' && rule.QuantityKey) {
            qKey = rule.QuantityKey;
        }
    });

    const currentQty = parseFloat(contextValues[qKey]) || 0;

    // 2. Determine bounds
    let maxTierQty = 100;
    blueprintState.Rules.forEach(rule => {
        if (rule.Type === 'TieredPricing' && rule.Tiers) {
            rule.Tiers.forEach(t => {
                if (t.MinQuantity > maxTierQty) maxTierQty = t.MinQuantity;
            });
        }
    });
    let maxQty = Math.max(maxTierQty * 1.5, currentQty * 1.2, 50);
    maxQty = Math.ceil(maxQty / 10) * 10; // Round to nearest 10

    // 3. Sample points
    const stepCount = 60;
    const dataPoints = [];
    let maxPrice = 0.0;
    for (let i = 0; i <= stepCount; i++) {
        const q = (maxQty / stepCount) * i;
        const p = simulatePriceForQty(q, qKey);
        dataPoints.push({ q, p });
        if (p > maxPrice) maxPrice = p;
    }

    if (maxPrice === 0) maxPrice = 100.0;
    const yMax = maxPrice * 1.15; // 15% top margin

    // 4. Coordinates helpers
    const padLeft = 40;
    const padRight = 15;
    const padTop = 15;
    const padBottom = 25;
    const chartWidth = width - padLeft - padRight;
    const chartHeight = height - padTop - padBottom;

    function getX(q) {
        return padLeft + (q / maxQty) * chartWidth;
    }
    function getY(p) {
        return padTop + chartHeight - (p / yMax) * chartHeight;
    }

    // 5. Draw horizontal gridlines & Y labels
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
    ctx.lineWidth = 1;
    ctx.fillStyle = '#6b7280';
    ctx.font = '9px Outfit, sans-serif';
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';

    const gridLines = 3;
    for (let i = 0; i <= gridLines; i++) {
        const ratio = i / gridLines;
        const val = yMax * ratio;
        const y = getY(val);

        ctx.beginPath();
        ctx.moveTo(padLeft, y);
        ctx.lineTo(width - padRight, y);
        ctx.stroke();

        ctx.fillText(`$${val.toFixed(0)}`, padLeft - 6, y);
    }

    // 6. Draw vertical ticks & X labels
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    const xTicks = 4;
    for (let i = 0; i <= xTicks; i++) {
        const ratio = i / xTicks;
        const q = maxQty * ratio;
        const x = getX(q);

        ctx.beginPath();
        ctx.moveTo(x, padTop + chartHeight);
        ctx.lineTo(x, padTop + chartHeight + 4);
        ctx.stroke();

        ctx.fillText(q.toFixed(0), x, padTop + chartHeight + 6);
    }

    // X-Axis Variable indicator
    ctx.fillStyle = '#9ca3af';
    ctx.textAlign = 'right';
    ctx.fillText(`${qKey} →`, width - padRight, padTop + chartHeight + 6);

    // 7. Plot Pricing Curve Line
    ctx.beginPath();
    ctx.moveTo(getX(dataPoints[0].q), getY(dataPoints[0].p));
    for (let i = 1; i < dataPoints.length; i++) {
        ctx.lineTo(getX(dataPoints[i].q), getY(dataPoints[i].p));
    }
    ctx.strokeStyle = '#6366f1'; // Indigo base line
    ctx.lineWidth = 2;
    ctx.stroke();

    // 8. Draw Min Price Floor dashed line if exists
    blueprintState.Rules.forEach(rule => {
        if (rule.Type === 'PriceCap' && rule.MinPrice != null) {
            const floorY = getY(rule.MinPrice);
            ctx.beginPath();
            ctx.strokeStyle = 'rgba(244, 63, 94, 0.4)';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 4]);
            ctx.moveTo(padLeft, floorY);
            ctx.lineTo(width - padRight, floorY);
            ctx.stroke();
            ctx.setLineDash([]); // Reset dashed state

            ctx.fillStyle = 'rgba(244, 63, 94, 0.8)';
            ctx.font = '8px Outfit, sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(`Floor Cap: $${rule.MinPrice.toFixed(0)}`, padLeft + 6, floorY - 6);
        }
    });

    // 9. Draw current scenario pointer dot
    const activePrice = parseFloat(finalPriceDisplay.textContent) || 0.0;
    const dotX = getX(currentQty);
    const dotY = getY(activePrice);

    // Vertical helper line
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.06)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(dotX, padTop);
    ctx.lineTo(dotX, padTop + chartHeight);
    ctx.stroke();

    // Outer glow circle
    ctx.beginPath();
    ctx.arc(dotX, dotY, 6, 0, 2 * Math.PI);
    ctx.fillStyle = '#06b6d4';
    ctx.fill();

    // Inner white core
    ctx.beginPath();
    ctx.arc(dotX, dotY, 2.5, 0, 2 * Math.PI);
    ctx.fillStyle = 'white';
    ctx.fill();

    // Active value label box
    ctx.fillStyle = 'white';
    ctx.font = 'bold 9px Outfit, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`$${activePrice.toFixed(2)}`, dotX, dotY - 10);
}

function renderTrace(steps) {
    traceContainer.innerHTML = '';
    
    if (steps.length === 0) {
        traceContainer.innerHTML = `<div style="text-align: center; color: var(--text-muted); font-size: 0.8rem; padding: 1rem;">No execution history yet.</div>`;
        return;
    }

    steps.forEach((step, idx) => {
        const item = document.createElement('div');
        item.className = `trace-step ${step.active ? 'active' : 'inactive'}`;

        let adjClass = step.adjustment > 0 ? 'pos' : (step.adjustment < 0 ? 'neg' : '');
        let adjSymbol = step.adjustment > 0 ? '+' : '';
        let adjText = step.adjustment !== 0 ? `${adjSymbol}$${step.adjustment.toFixed(2)}` : '$0.00';

        item.innerHTML = `
            <div class="trace-indicator">${idx + 1}</div>
            <div class="trace-details">
                <div class="trace-step-title">${step.ruleName || 'Unnamed Rule'}</div>
                <div class="trace-step-desc">${step.description}</div>
                ${step.active ? `
                    <div class="trace-calc-flow">
                        <span>$${step.inputPrice.toFixed(2)}</span>
                        <span class="adjustment-amount ${adjClass}">${adjText}</span>
                        <span>$${step.outputPrice.toFixed(2)}</span>
                    </div>
                ` : ''}
            </div>
        `;
        traceContainer.appendChild(item);
    });
}

// 7. Developer Hub Generators
function generateCSharpCode() {
    let code = `using System;
using Forge;

class Program
{
    static void Main()
    {
        // Fluent API C# representation of the blueprint
        var blueprint = new BlueprintBuilder("${blueprintState.BlueprintName}")\n`;

    blueprintState.Rules.forEach(rule => {
        if (rule.Type === 'BasePrice') {
            code += `            .SetBasePrice("${rule.Name}", ${rule.DefaultPrice.toFixed(1)}, "${rule.ContextKey || 'base_price'}")\n`;
        }
        else if (rule.Type === 'PercentageAdjustment') {
            code += `            .AddPercentageAdjustment("${rule.Name}", ${rule.Factor.toFixed(2)}, "${rule.ConditionKey || ''}", "${rule.Description || ''}")\n`;
        }
        else if (rule.Type === 'FlatAdjustment') {
            code += `            .AddFlatAdjustment("${rule.Name}", ${rule.Amount.toFixed(1)}, "${rule.ConditionKey || ''}", "${rule.Description || ''}")\n`;
        }
        else if (rule.Type === 'PriceCap') {
            let minVal = rule.MinPrice !== null && rule.MinPrice !== undefined ? `${rule.MinPrice.toFixed(1)}` : 'null';
            let maxVal = rule.MaxPrice !== null && rule.MaxPrice !== undefined ? `${rule.MaxPrice.toFixed(1)}` : 'null';
            code += `            .AddPriceCap("${rule.Name}", ${minVal}, ${maxVal})\n`;
        }
        else if (rule.Type === 'TieredPricing') {
            code += `            .AddVolumeTiers("${rule.Name}", "${rule.QuantityKey || 'quantity'}"`;
            (rule.Tiers || []).forEach(tier => {
                code += `, (${tier.MinQuantity}, ${tier.DiscountPercentage.toFixed(2)})`;
            });
            code += `)\n`;
        }
    });

    code += `            .Build();\n\n`;
    code += `        Console.WriteLine($"Successfully assembled blueprint: {blueprint.BlueprintName}");\n`;
    code += `    }\n}`;

    csharpOutput.textContent = code;
}

function generateCppCode() {
    let code = `// C++ Integration Boilerplate
#include "PriceBlueprint.h"
#include <iostream>
#include <memory>

int main() {
    using namespace Pricing;

    // Load blueprint dynamically from JSON file
    std::shared_ptr<PriceBlueprint> blueprint;
    try {
        blueprint = PriceBlueprint::LoadFromFile("enterprise_blueprint.json");
        std::cout << "Blueprint '" << blueprint->GetName() << "' loaded.\\n";
    } catch(const std::exception& e) {
        std::cerr << "Load error: " << e.what() << "\\n";
        return 1;
    }

    // Set up inputs in the scenario context
    PricingContext context;
    context.Set("base_price", ${contextValues.base_price ? contextValues.base_price.toFixed(1) : '250.0'});
    context.Set("quantity", ${contextValues.quantity ? contextValues.quantity.toFixed(1) : '120.0'});
    context.Set("region", std::string("${contextValues.region || 'US'}"));
    context.Set("is_partner", ${contextValues.is_partner ? 'true' : 'false'});

    // Evaluate the blueprint pipeline
    PriceResult result = blueprint->Calculate(context);
    
    // Display results report
    result.Print();

    return 0;
}`;
    cppOutput.textContent = code;
}

// 8. Bidirectional Sync & Editors
function onVisualChange(reRenderDesigner = true) {
    // Generate JSON and YAML strings
    const jsonStr = JSON.stringify(blueprintState, null, 2);
    jsonEditor.value = jsonStr;
    
    try {
        if (window.jsyaml) {
            yamlEditor.value = jsyaml.dump(blueprintState, { indent: 2, lineWidth: -1 });
        }
    } catch(e) {
        console.error("YAML serialization error", e);
    }

    generateCSharpCode();
    generateCppCode();
    runSimulator();
    if (reRenderDesigner) {
        renderVisualDesigner();
    }
    setValid(true, "Specification synced & validated successfully.");
}

function onJSONEditorInput() {
    const rawVal = jsonEditor.value;
    try {
        const parsed = JSON.parse(rawVal);
        
        // Quick structural check
        if (!parsed.BlueprintName || !Array.isArray(parsed.Rules)) {
            throw new Error("Must contain a 'BlueprintName' (string) and 'Rules' (array).");
        }
        
        blueprintState = parsed;
        collapsedRules.clear();
        renderVisualDesigner();
        
        // Generate YAML & APIs
        try {
            if (window.jsyaml) {
                yamlEditor.value = jsyaml.dump(blueprintState, { indent: 2, lineWidth: -1 });
            }
        } catch(e) {}
        
        generateCSharpCode();
        generateCppCode();
        renderContextInputs();
        runSimulator();
        
        setValid(true, "JSON spec parsed & synced successfully.");
    } catch (e) {
        setValid(false, `JSON parsing error: ${e.message}`);
    }
}

function onYAMLEditorInput() {
    const rawVal = yamlEditor.value;
    try {
        if (!window.jsyaml) {
            throw new Error("YAML parser library not loaded.");
        }
        
        const parsed = jsyaml.load(rawVal);
        if (!parsed || !parsed.BlueprintName || !Array.isArray(parsed.Rules)) {
            throw new Error("Must contain a 'BlueprintName' (string) and 'Rules' (array).");
        }
        
        blueprintState = parsed;
        collapsedRules.clear();
        renderVisualDesigner();
        
        // Generate JSON & APIs
        jsonEditor.value = JSON.stringify(blueprintState, null, 2);
        
        generateCSharpCode();
        generateCppCode();
        renderContextInputs();
        runSimulator();
        
        setValid(true, "YAML spec parsed & synced successfully.");
    } catch (e) {
        setValid(false, `YAML parsing error: ${e.message}`);
    }
}

function setValid(isValid, message) {
    if (isValid) {
        validationBar.className = 'validation-bar valid';
        validationMessage.textContent = message;
        iconValid.classList.remove('hidden');
        iconInvalid.classList.add('hidden');
    } else {
        validationBar.className = 'validation-bar invalid';
        validationMessage.textContent = message;
        iconValid.classList.add('hidden');
        iconInvalid.classList.remove('hidden');
    }
}

// 9. Server Operations
function loadBlueprintFromDisk() {
    setValid(true, "Loading specification from disk...");
    fetch('/api/blueprint')
        .then(res => {
            if (!res.ok) throw new Error("Server returned error status " + res.status);
            return res.json();
        })
        .then(data => {
            if (data.BlueprintName && Array.isArray(data.Rules)) {
                blueprintState = data;
                collapsedRules.clear();
                onVisualChange();
                renderContextInputs();
                runSimulator();
                setValid(true, "Specification loaded and validated successfully.");
            } else {
                throw new Error("Invalid blueprint format returned from API.");
            }
        })
        .catch(err => {
            console.warn("Could not load from API, using default embedded template.", err);
            // Setup default rules list as fallback template
            blueprintState = {
                BlueprintName: "Enterprise Custom Contract",
                Rules: [
                    DEFAULT_RULES.BasePrice(),
                    DEFAULT_RULES.TieredPricing(),
                    DEFAULT_RULES.PercentageAdjustment(),
                    DEFAULT_RULES.FlatAdjustment(),
                    DEFAULT_RULES.PercentageAdjustment(), // Tax surcharge
                    DEFAULT_RULES.PriceCap()
                ]
            };
            // Customize default template values slightly
            blueprintState.Rules[0].DefaultPrice = 250.0;
            blueprintState.Rules[2].Name = "Partner Discount";
            blueprintState.Rules[2].Factor = 0.90;
            blueprintState.Rules[2].ConditionKey = "is_partner";
            blueprintState.Rules[2].Description = "Partner Channel 10% discount";
            blueprintState.Rules[3].Name = "Shipping & Handling Surcharge";
            blueprintState.Rules[3].Amount = 15.0;
            blueprintState.Rules[3].ConditionKey = "quantity < 10";
            blueprintState.Rules[3].Description = "Flat rate standard shipping fee for small orders";
            blueprintState.Rules[4].Name = "International Tax Surcharge";
            blueprintState.Rules[4].Factor = 1.15;
            blueprintState.Rules[4].ConditionKey = "region != US";
            blueprintState.Rules[4].Description = "International regional sales tax of 15%";
            
            onVisualChange();
            renderContextInputs();
            runSimulator();
            
            const connectionStatus = document.getElementById('connection-status');
            connectionStatus.className = 'status-indicator';
            connectionStatus.querySelector('.status-text').textContent = 'Local Mode (Standalone)';
            setValid(true, "Offline model initialized with default pricing spec.");
        });
}

function saveBlueprintToDisk() {
    setValid(true, "Saving changes to disk...");
    
    // Validate JSON structure first
    let payload = null;
    try {
        payload = JSON.stringify(blueprintState);
    } catch(e) {
        setValid(false, "Failed to serialize blueprint for saving.");
        return;
    }

    fetch('/api/blueprint', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: payload
    })
    .then(res => {
        if (!res.ok) return res.json().then(d => { throw new Error(d.error || "Save error"); });
        return res.json();
    })
    .then(data => {
        setValid(true, "Blueprint saved successfully.");
        showToast();
    })
    .catch(err => {
        setValid(false, `Failed to save: ${err.message}`);
        alert("Failed to save changes. Make sure the local web server is running and writable.\n\nError: " + err.message);
    });
}

function showToast() {
    toast.classList.remove('hidden');
    setTimeout(() => {
        toast.classList.add('hidden');
    }, 2000);
}

// 10. Startup
window.addEventListener('DOMContentLoaded', () => {
    initEvents();
    loadBlueprintFromDisk();
});
