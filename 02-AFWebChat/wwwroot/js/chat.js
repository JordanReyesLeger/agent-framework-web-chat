// =============================================
// AF-WebChat — Chat JavaScript
// SSE streaming, agent sidebar, tool cards,
// approval dialog, document upload
// =============================================

const state = {
    currentAgent: 'GeneralAssistant',
    currentWorkflow: null,
    currentOrchestration: null,
    sessionId: generateId(),
    messageCount: 0,
    streaming: false,
    agents: [],
    workflows: [],
    orchestrations: [],
    attachedDocument: null,
    builderActive: false,
    builderPattern: 'Sequential',
    builderAgents: [],
    currentMessages: []
};

// ---- Initialization ----
document.addEventListener('DOMContentLoaded', async () => {
    // Configure marked.js
    if (typeof marked !== 'undefined') {
        marked.setOptions({
            highlight: (code, lang) => {
                if (typeof hljs !== 'undefined' && lang && hljs.getLanguage(lang)) {
                    return hljs.highlight(code, { language: lang }).value;
                }
                return code;
            },
            breaks: true
        });
    }

    await loadAgents();
    await loadOrchestrations();
    await loadWorkflows();
    injectNavbarChatActions();
    setupEventListeners();
    updateSessionBadge();
    updateAgentContext();
    renderExamplePrompts();
    loadSessionHistory();
});

function injectNavbarChatActions() {
    const container = document.getElementById('navbarChatActions');
    if (!container) return;
    container.innerHTML = `
        <button class="btn btn-sm af-nav-btn" id="btnSessionHistory" title="Session history" onclick="toggleSessionDrawer()">
            <i class="bi bi-clock-history"></i>
        </button>
        <button class="btn btn-sm af-nav-btn" id="btnNewSession" title="New session">
            <i class="bi bi-plus-circle"></i> New
        </button>
        <span class="af-nav-session-badge" id="sessionBadge"></span>
    `;
}

// ---- Load Data ----
async function loadAgents() {
    try {
        const res = await fetch('/api/agent');
        state.agents = await res.json();
        renderAgentList();
    } catch (e) {
        console.error('Failed to load agents:', e);
    }
}

async function loadOrchestrations() {
    try {
        const res = await fetch('/api/orchestration');
        state.orchestrations = await res.json();
        renderOrchestrationList();
    } catch (e) {
        console.error('Failed to load orchestrations:', e);
    }
}

async function loadWorkflows() {
    try {
        const res = await fetch('/api/workflow');
        state.workflows = await res.json();
        renderWorkflowList();
    } catch (e) {
        console.error('Failed to load workflows:', e);
    }
}

// ---- Render ----
function renderAgentList() {
    const list = document.getElementById('agentList');
    const filter = document.getElementById('agentFilter').value.toLowerCase();

    list.innerHTML = state.agents
        .filter(a => a.name.toLowerCase().includes(filter) || a.description.toLowerCase().includes(filter) || a.category.toLowerCase().includes(filter))
        .map(a => {
            const addBtn = state.builderActive
                ? `<button class="af-agent-add-btn" onclick="event.stopPropagation(); addAgentToBuilder('${a.name}')" title="Add to workflow"><i class="bi bi-plus-lg"></i></button>`
                : '';
            return `
            <div class="af-agent-card ${a.name === state.currentAgent ? 'active' : ''}"
                 data-agent="${a.name}" onclick="selectAgent('${a.name}')">
                <div class="af-agent-card-header">
                    <div class="af-agent-card-icon" style="background-color: ${a.color || '#0078d4'}">
                        ${a.icon}
                    </div>
                    <div class="af-agent-card-info">
                        <div class="af-agent-card-name">${escapeHtml(a.name)}</div>
                        <div class="af-agent-card-desc">${escapeHtml(a.description)}</div>
                    </div>
                    ${addBtn}
                </div>
            </div>
        `;}).join('');
}

function renderWorkflowList() {
    const list = document.getElementById('workflowList');
    list.innerHTML = state.workflows.map(w => {
        const patternIcon = getPatternIcon(w.pattern);
        const agentPips = (w.agents || []).map(aName => {
            const a = state.agents.find(x => x.name === aName);
            const color = a?.color || '#0078d4';
            const icon = a?.icon || '\uD83E\uDD16';
            return `<span class="af-wf-card-agent-pip" style="background:${color}" title="${escapeHtml(aName)}">${icon}</span>`;
        }).join('');
        return `
            <li class="af-wf-card ${w.name === state.currentWorkflow ? 'active' : ''}"
                data-workflow="${w.name}" onclick="selectWorkflow('${w.name}')">
                <div class="af-wf-card-header">
                    <span class="af-wf-card-icon">${patternIcon}</span>
                    <div class="af-wf-card-info">
                        <div class="af-wf-card-name">${escapeHtml(w.name)}</div>
                        <div class="af-wf-card-pattern">${escapeHtml(w.pattern)}</div>
                    </div>
                </div>
                <div class="af-wf-card-agents">${agentPips}</div>
            </li>`;
    }).join('');
}

function renderOrchestrationList() {
    const list = document.getElementById('orchestrationList');
    if (!list) return;
    list.innerHTML = state.orchestrations.map(o => {
        const patternIcon = getPatternIcon(o.orchestrationPattern);
        const agentPips = (o.agents || []).map(aName => {
            const a = state.agents.find(x => x.name === aName);
            const color = a?.color || '#0078d4';
            const icon = a?.icon || '\uD83E\uDD16';
            return `<span class="af-wf-card-agent-pip" style="background:${color}" title="${escapeHtml(aName)}">${icon}</span>`;
        }).join('');
        return `
            <li class="af-wf-card ${o.name === state.currentOrchestration ? 'active' : ''}"
                data-orchestration="${o.name}" onclick="selectOrchestration('${o.name}')">
                <div class="af-wf-card-header">
                    <span class="af-wf-card-icon">${patternIcon}</span>
                    <div class="af-wf-card-info">
                        <div class="af-wf-card-name">${escapeHtml(o.name)}</div>
                        <div class="af-wf-card-pattern">${escapeHtml(o.orchestrationPattern)}</div>
                    </div>
                </div>
                <div class="af-wf-card-agents">${agentPips}</div>
            </li>`;
    }).join('');
}

function selectOrchestration(name) {
    state.currentOrchestration = name;
    state.currentWorkflow = null;
    renderOrchestrationList();
    renderWorkflowList();
    renderOrchestrationFlow();
}

function renderOrchestrationFlow() {
    const flowEl = document.getElementById('workflowFlow');
    const ctxEl = document.getElementById('agentContext');
    const promptsEl = document.getElementById('examplePrompts');
    if (!flowEl) return;

    const orch = state.orchestrations.find(o => o.name === state.currentOrchestration);
    if (!orch) {
        flowEl.style.display = 'none';
        updateAgentContext();
        renderExamplePrompts();
        return;
    }

    if (ctxEl) ctxEl.style.display = 'none';
    if (promptsEl) promptsEl.style.display = 'none';

    flowEl.style.display = 'block';
    document.getElementById('wfFlowName').textContent = orch.name;
    document.getElementById('wfFlowPattern').textContent = orch.orchestrationPattern + ' (AF Orchestration)';
    document.getElementById('wfFlowDesc').textContent = orch.description;

    const canvas = document.getElementById('wfFlowCanvas');
    canvas.className = 'af-wf-flow-canvas';

    switch (orch.orchestrationPattern) {
        case 'Sequential':
            canvas.classList.add('af-wf-canvas-sequential');
            canvas.innerHTML = renderSequentialFlow(orch.agents);
            break;
        case 'Concurrent':
            canvas.classList.add('af-wf-canvas-fanout');
            canvas.innerHTML = renderConcurrentFlow(orch.agents);
            break;
        case 'GroupChat':
            canvas.classList.add('af-wf-canvas-groupchat');
            canvas.innerHTML = renderGroupChatFlow(orch.agents);
            break;
        case 'GroupChatAI':
            canvas.classList.add('af-wf-canvas-groupchat');
            canvas.innerHTML = renderGroupChatAIFlow(orch.agents);
            break;
        case 'Handoff':
            canvas.classList.add('af-wf-canvas-conditional');
            canvas.innerHTML = renderHandoffFlow(orch.agents);
            break;
        default:
            canvas.classList.add('af-wf-canvas-sequential');
            canvas.innerHTML = renderSequentialFlow(orch.agents);
    }
}

function getPatternIcon(pattern) {
    switch (pattern) {
        case 'Sequential': return '<i class="bi bi-arrow-right"></i>';
        case 'Concurrent': return '<i class="bi bi-diagram-3"></i>';
        case 'Handoff': return '<i class="bi bi-signpost-split"></i>';
        case 'GroupChat': return '<i class="bi bi-people"></i>';
        case 'GroupChatAI': return '<i class="bi bi-robot"></i>';
        case 'Iterative': return '<i class="bi bi-arrow-repeat"></i>';
        case 'Conditional': return '<i class="bi bi-signpost-split"></i>';
        case 'FanOut': return '<i class="bi bi-diagram-3"></i>';
        default: return '<i class="bi bi-diagram-3"></i>';
    }
}

// ---- Sidebar Tabs ----
function switchSidebarTab(tab) {
    document.querySelectorAll('.af-side-tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tab));
    document.getElementById('tabPanelAgents').style.display = tab === 'agents' ? '' : 'none';
    document.getElementById('tabPanelCatalog').style.display = tab === 'catalog' ? '' : 'none';
    document.getElementById('tabPanelBuilder').style.display = tab === 'builder' ? '' : 'none';
    state.builderActive = (tab === 'builder');
    if (tab === 'builder') {
        loadStudioTools();
        renderBuilderAgentPicker();
    }
}

function switchCatalogSub(sub) {
    document.querySelectorAll('.af-cat-sub').forEach(t => t.classList.toggle('active', t.dataset.sub === sub));
    document.getElementById('subOrch').style.display = sub === 'orch' ? '' : 'none';
    document.getElementById('subWf').style.display = sub === 'wf' ? '' : 'none';
}

function renderBuilderAgentPicker() {
    const el = document.getElementById('builderAgentPicker');
    if (!el) return;
    const f = (document.getElementById('builderAgentFilter')?.value || '').toLowerCase();
    el.innerHTML = state.agents
        .filter(a => a.name.toLowerCase().includes(f) || a.description.toLowerCase().includes(f))
        .map(a => `
            <div class="af-bld-pick" onclick="addAgentToBuilder('${a.name}')">
                <div class="af-bld-pick-icon" style="background:${a.color || '#0078d4'}">${a.icon}</div>
                <span class="af-bld-pick-name">${escapeHtml(a.name)}</span>
                <button class="af-bld-pick-add"><i class="bi bi-plus"></i></button>
            </div>`).join('');
}

// ---- Workflow Builder (legacy) ----
function switchWorkflowTab(tab) { }

function selectBuilderPattern(pattern) {
    state.builderPattern = pattern;
    document.querySelectorAll('.af-flow-pat').forEach(b => {
        b.classList.toggle('active', b.dataset.pattern === pattern);
    });
    renderBuilderAgentChips();
    renderInlineFlowCanvas();
    renderBuilderPreview();
}

function addAgentToBuilder(agentName) {
    // Allow duplicates (agent can appear in sequential more than once)
    state.builderAgents.push(agentName);
    renderBuilderAgentChips();
    renderInlineFlowCanvas();
    renderBuilderPreview();
}

function removeAgentFromBuilder(index) {
    state.builderAgents.splice(index, 1);
    renderBuilderAgentChips();
    renderInlineFlowCanvas();
    renderBuilderPreview();
}

function moveBuilderAgent(fromIndex, direction) {
    const toIndex = fromIndex + direction;
    if (toIndex < 0 || toIndex >= state.builderAgents.length) return;
    const arr = state.builderAgents;
    [arr[fromIndex], arr[toIndex]] = [arr[toIndex], arr[fromIndex]];
    renderBuilderAgentChips();
    renderInlineFlowCanvas();
    renderBuilderPreview();
}

function renderBuilderAgentChips() {
    const container = document.getElementById('builderAgents');
    const placeholder = document.getElementById('builderPlaceholder');
    const useBtn = document.getElementById('btnUseCustomWf');
    const countBadge = document.getElementById('flowStepCount');

    if (countBadge) countBadge.textContent = state.builderAgents.length;

    if (state.builderAgents.length === 0) {
        if (placeholder) placeholder.style.display = 'flex';
        container.querySelectorAll('.af-flow-step').forEach(c => c.remove());
        if (useBtn) useBtn.disabled = true;
        return;
    }

    if (placeholder) placeholder.style.display = 'none';
    if (useBtn) useBtn.disabled = false;

    const chips = state.builderAgents.map((name, i) => {
        const a = state.agents.find(x => x.name === name);
        const color = a?.color || '#0078d4';
        const icon = a?.icon || '\uD83E\uDD16';
        const stepNum = i + 1;
        return `<div class="af-flow-step" data-idx="${i}">
            <div class="af-flow-step-number">${stepNum}</div>
            <div class="af-flow-step-icon" style="background:${color}">${icon}</div>
            <div class="af-flow-step-info">
                <span class="af-flow-step-name">${escapeHtml(name)}</span>
                <span class="af-flow-step-role">${getStepRoleLabel(i, state.builderPattern)}</span>
            </div>
            <div class="af-flow-step-actions">
                <button onclick="moveBuilderAgent(${i}, -1)" ${i === 0 ? 'disabled' : ''} title="Move up"><i class="bi bi-chevron-up"></i></button>
                <button onclick="moveBuilderAgent(${i}, 1)" ${i === state.builderAgents.length - 1 ? 'disabled' : ''} title="Move down"><i class="bi bi-chevron-down"></i></button>
                <button onclick="removeAgentFromBuilder(${i})" title="Remove" class="af-flow-step-remove"><i class="bi bi-x-lg"></i></button>
            </div>
        </div>`;
    }).join('');

    // Keep placeholder hidden, replace chip content
    const existingChips = container.querySelectorAll('.af-flow-step');
    existingChips.forEach(c => c.remove());
    container.insertAdjacentHTML('beforeend', chips);
}

// Get a role label for the step based on pattern and position
function getStepRoleLabel(index, pattern) {
    const total = state.builderAgents.length;
    switch (pattern) {
        case 'Sequential':
            if (index === 0) return 'Start';
            if (index === total - 1) return 'End';
            return `Step ${index + 1}`;
        case 'Conditional':
            if (index === 0) return 'AI Classifier — picks branch';
            if (index === total - 1 && total > 2) return `Option ${index} (Fallback)`;
            return `Option ${index}`;
        case 'Handoff':
            if (index === 0) return 'AI Triage — picks specialist';
            return `Specialist ${index}`;
        case 'Concurrent':
            return 'Parallel';
        case 'FanOut':
            return 'Worker';
        case 'GroupChat':
        case 'GroupChatAI':
            return 'Participant';
        case 'Iterative':
            if (index === 0) return 'Writer';
            if (index === 1) return 'Reviewer';
            return 'Observer';
        default:
            return `Step ${index + 1}`;
    }
}

// ---- Inline Flow Canvas (visual preview inside sidebar) ----
function renderInlineFlowCanvas() {
    const canvas = document.getElementById('flowInlineCanvas');
    if (!canvas) return;

    if (state.builderAgents.length === 0) {
        canvas.innerHTML = `<div class="af-flow-empty-state">
            <i class="bi bi-diagram-3"></i>
            <span>Add agents below to build your flow</span>
        </div>`;
        return;
    }

    const agents = state.builderAgents;
    let html = '';

    switch (state.builderPattern) {
        case 'Sequential':
            html = renderInlineSequential(agents);
            break;
        case 'Concurrent':
            html = renderInlineConcurrent(agents);
            break;
        case 'Conditional':
        case 'Handoff':
            html = renderInlineConditional(agents);
            break;
        case 'GroupChat':
        case 'GroupChatAI':
            html = renderInlineGroupChat(agents);
            break;
        case 'Iterative':
            html = renderInlineIterative(agents);
            break;
        case 'FanOut':
            html = renderInlineConcurrent(agents);
            break;
        default:
            html = renderInlineSequential(agents);
    }

    canvas.innerHTML = html;
}

function inlineNodeHtml(agentName, stepIndex, extraClass) {
    const a = state.agents.find(x => x.name === agentName);
    const color = a?.color || '#0078d4';
    const icon = a?.icon || '\uD83E\uDD16';
    return `<div class="af-iflow-node ${extraClass || ''}" data-flow-step="${stepIndex}" data-flow-agent="${escapeHtml(agentName)}">
        <div class="af-iflow-node-icon" style="background:${color}">${icon}</div>
        <div class="af-iflow-node-name">${escapeHtml(agentName)}</div>
        <div class="af-iflow-node-status"></div>
    </div>`;
}

function renderInlineSequential(agents) {
    let html = '<div class="af-iflow-seq">';
    agents.forEach((a, i) => {
        html += inlineNodeHtml(a, i);
        if (i < agents.length - 1) {
            html += '<div class="af-iflow-connector"><i class="bi bi-arrow-down"></i></div>';
        }
    });
    html += '</div>';
    return html;
}

function renderInlineConcurrent(agents) {
    let html = '<div class="af-iflow-label"><i class="bi bi-play-fill"></i> Start</div>';
    html += '<div class="af-iflow-connector"><i class="bi bi-arrow-down"></i></div>';
    html += '<div class="af-iflow-parallel">';
    agents.forEach((a, i) => {
        html += inlineNodeHtml(a, i);
    });
    html += '</div>';
    html += '<div class="af-iflow-connector"><i class="bi bi-arrow-down"></i></div>';
    html += '<div class="af-iflow-label"><i class="bi bi-check-circle"></i> Aggregate</div>';
    return html;
}

function renderInlineConditional(agents) {
    if (agents.length === 0) return '';
    const triage = agents[0];
    const branches = agents.slice(1);
    const isHandoff = state.builderPattern === 'Handoff';
    const routerLabel = isHandoff ? 'Triage Agent' : 'Classifier Agent';
    const routerDesc = isHandoff
        ? 'The AI reads the message and hands off to the best specialist automatically'
        : 'The AI analyzes the message and picks the best branch automatically';

    let html = '';
    // Router header with explanation
    html += `<div class="af-iflow-cond-header">
        <div class="af-iflow-cond-badge"><i class="bi bi-${isHandoff ? 'signpost-split' : 'shuffle'}"></i> ${routerLabel}</div>
        <div class="af-iflow-cond-explain">${routerDesc}</div>
    </div>`;
    html += inlineNodeHtml(triage, 0, 'af-iflow-router');

    if (branches.length > 0) {
        html += '<div class="af-iflow-cond-decision"><i class="bi bi-robot"></i> AI decides</div>';
        html += '<div class="af-iflow-cond-lines"></div>';
        html += '<div class="af-iflow-branches">';
        branches.forEach((a, i) => {
            const condLabel = isHandoff
                ? `Specialist ${i + 1}`
                : (i === branches.length - 1 && branches.length > 1 ? 'Fallback' : `Option ${i + 1}`);
            html += `<div class="af-iflow-branch">
                <div class="af-iflow-branch-label">${condLabel}</div>
                <div class="af-iflow-branch-line"></div>
                ${inlineNodeHtml(a, i + 1)}
            </div>`;
        });
        html += '</div>';
        html += `<div class="af-iflow-cond-footer"><i class="bi bi-lightbulb"></i> No rules needed — AI picks the best match</div>`;
    } else {
        html += '<div class="af-iflow-cond-footer"><i class="bi bi-plus-circle"></i> Add agents below to create branches</div>';
    }
    return html;
}

function renderInlineGroupChat(agents) {
    let html = '<div class="af-iflow-group">';
    html += '<div class="af-iflow-group-ring">';
    agents.forEach((a, i) => {
        html += inlineNodeHtml(a, i, 'af-iflow-group-member');
    });
    html += '</div>';
    html += '<div class="af-iflow-group-label"><i class="bi bi-arrow-repeat"></i> Round-robin</div>';
    html += '</div>';
    return html;
}

function renderInlineIterative(agents) {
    const writer = agents[0] || agents[0];
    const reviewer = agents[1] || agents[agents.length > 1 ? 1 : 0];

    let html = '<div class="af-iflow-loop">';
    html += inlineNodeHtml(writer, 0);
    html += '<div class="af-iflow-loop-arrow"><i class="bi bi-arrow-right"></i></div>';
    if (agents.length > 1) {
        html += inlineNodeHtml(reviewer, 1);
        html += '<div class="af-iflow-loop-back"><i class="bi bi-arrow-return-left"></i> Loop</div>';
    }
    html += '</div>';
    return html;
}

// ---- Flow Preview (updates flow name) ----
function updateFlowPreview() {
    renderInlineFlowCanvas();
}

function renderBuilderPreview() {
    if (state.builderAgents.length === 0) {
        const flowEl = document.getElementById('workflowFlow');
        if (flowEl) flowEl.style.display = 'none';
        return;
    }

    // Build a virtual workflow and render it using existing flow renderer
    const virtualWf = {
        name: 'Custom Workflow',
        description: `${state.builderPattern} pattern with ${state.builderAgents.length} agents`,
        agents: [...state.builderAgents],
        pattern: state.builderPattern
    };

    // Temporarily set it so renderWorkflowFlow can use it
    state.currentWorkflow = '__custom__';
    state._customWorkflow = virtualWf;
    renderWorkflowFlow();
}

function clearBuilder() {
    state.builderAgents = [];
    state.currentWorkflow = null;
    state._customWorkflow = null;
    renderBuilderAgentChips();
    renderInlineFlowCanvas();
    hideFlowProgress();
    const flowEl = document.getElementById('workflowFlow');
    if (flowEl) flowEl.style.display = 'none';
    updateAgentContext();
    renderExamplePrompts();
}

function activateCustomWorkflow() {
    if (state.builderAgents.length === 0) return;
    // The custom workflow is already previewed; just make sure state is set
    state.currentWorkflow = '__custom__';
    state._customWorkflow = {
        name: document.getElementById('flowName')?.value?.trim() || 'Custom Flow',
        description: `${state.builderPattern} pattern with ${state.builderAgents.length} agents`,
        agents: [...state.builderAgents],
        pattern: state.builderPattern
    };
    renderBuilderPreview();
    showFlowProgress();
    showStudioToast(`Flow "${state._customWorkflow.name}" is now active! Send a message to start.`, 'success');
}

// ---- Agent Selection ----
function selectAgent(name) {
    state.currentAgent = name;
    state.currentWorkflow = null;
    state.currentOrchestration = null;
    renderAgentList();
    renderWorkflowList();
    renderOrchestrationList();
    updateAgentInfo(name);
    // Hide workflow flow, show agent context
    const flowEl = document.getElementById('workflowFlow');
    if (flowEl) flowEl.style.display = 'none';
    updateAgentContext();
    renderExamplePrompts();
}

function selectWorkflow(name) {
    state.currentWorkflow = name;
    state.currentOrchestration = null;
    renderWorkflowList();
    renderOrchestrationList();
    renderWorkflowFlow();
}

function renderWorkflowFlow() {
    const flowEl = document.getElementById('workflowFlow');
    const ctxEl = document.getElementById('agentContext');
    const promptsEl = document.getElementById('examplePrompts');
    if (!flowEl) return;

    const wf = state.currentWorkflow === '__custom__'
        ? state._customWorkflow
        : state.workflows.find(w => w.name === state.currentWorkflow);
    if (!wf) {
        flowEl.style.display = 'none';
        // Show agent context instead
        updateAgentContext();
        renderExamplePrompts();
        return;
    }

    // Hide agent context when workflow is shown
    if (ctxEl) ctxEl.style.display = 'none';
    if (promptsEl) promptsEl.style.display = 'none';

    flowEl.style.display = 'block';
    document.getElementById('wfFlowName').textContent = wf.name;
    document.getElementById('wfFlowPattern').textContent = wf.pattern;
    document.getElementById('wfFlowDesc').textContent = wf.description;

    const canvas = document.getElementById('wfFlowCanvas');
    canvas.className = 'af-wf-flow-canvas';

    switch (wf.pattern) {
        case 'Sequential':
            canvas.classList.add('af-wf-canvas-sequential');
            canvas.innerHTML = renderSequentialFlow(wf.agents);
            break;
        case 'Conditional':
            canvas.classList.add('af-wf-canvas-conditional');
            canvas.innerHTML = renderConditionalFlow(wf.agents);
            break;
        case 'FanOut':
            canvas.classList.add('af-wf-canvas-fanout');
            canvas.innerHTML = renderFanOutFlow(wf.agents);
            break;
        case 'Iterative':
            canvas.classList.add('af-wf-canvas-iterative');
            canvas.innerHTML = renderIterativeFlow(wf.agents);
            break;
        case 'GroupChat':
            canvas.classList.add('af-wf-canvas-groupchat');
            canvas.innerHTML = renderGroupChatFlow(wf.agents);
            break;
        case 'Handoff':
            canvas.classList.add('af-wf-canvas-conditional');
            canvas.innerHTML = renderHandoffFlow(wf.agents);
            break;
        case 'Concurrent':
            canvas.classList.add('af-wf-canvas-fanout');
            canvas.innerHTML = renderConcurrentFlow(wf.agents);
            break;
        default:
            canvas.classList.add('af-wf-canvas-sequential');
            canvas.innerHTML = renderSequentialFlow(wf.agents);
    }
}

function buildNodeHtml(agentName, extraClass) {
    const a = state.agents.find(x => x.name === agentName);
    const color = a?.color || '#0078d4';
    const icon = a?.icon || '\uD83E\uDD16';
    return `<div class="af-wf-node idle ${extraClass || ''}" data-wf-agent="${escapeHtml(agentName)}">
        <div class="af-wf-node-icon" style="background:${color}">${icon}</div>
        <span class="af-wf-node-name">${escapeHtml(agentName)}</span>
    </div>`;
}

function arrowHtml() {
    return '<div class="af-wf-arrow"><i class="bi bi-arrow-right"></i></div>';
}

function renderSequentialFlow(agents) {
    return agents.map((a, i) => {
        let html = buildNodeHtml(a);
        if (i < agents.length - 1) html += arrowHtml();
        return html;
    }).join('');
}

function renderConditionalFlow(agents) {
    const classifier = agents[0];
    const branches = agents.slice(1);
    return `
        <div class="af-wf-cond-explain-bar">
            <i class="bi bi-robot"></i>
            <span>The <strong>1st agent</strong> (Classifier) reads the user message and <strong>automatically picks</strong> the best branch — no rules needed</span>
        </div>
        <div class="af-wf-cond-how-box">
            <div class="af-wf-cond-how-title"><i class="bi bi-gear"></i> How it works</div>
            <ol class="af-wf-cond-how-steps">
                <li>User sends a message</li>
                <li><strong>${escapeHtml(classifier)}</strong> analyzes the message using AI</li>
                <li>AI picks the best matching agent from the branches</li>
                <li>Only that agent responds</li>
            </ol>
        </div>
        <div class="af-wf-cond-router">
            <div class="af-wf-cond-router-badge"><i class="bi bi-robot"></i> AI Classifier</div>
            ${buildNodeHtml(classifier)}
        </div>
        <div class="af-wf-arrow-down"><i class="bi bi-arrow-down"></i></div>
        <div class="af-wf-cond-decision-diamond">
            <i class="bi bi-question-diamond-fill"></i>
        </div>
        <div class="af-wf-cond-branches">
            ${branches.map((a, i) => `
                <div class="af-wf-cond-branch">
                    <div class="af-wf-cond-branch-tag">Option ${i + 1}${i === branches.length - 1 && branches.length > 1 ? ' (Fallback)' : ''}</div>
                    <div class="af-wf-cond-branch-line"></div>
                    ${buildNodeHtml(a)}
                </div>
            `).join('')}
        </div>
        <div class="af-wf-groupchat-label"><i class="bi bi-lightbulb"></i> No manual rules — the AI classifier decides which agent is best for each message</div>
    `;
}

function renderFanOutFlow(agents) {
    return `
        <div class="af-wf-fanout-parallel">
            ${agents.map(a => buildNodeHtml(a)).join('')}
        </div>
        <div class="af-wf-fanout-merge">
            <div class="af-wf-fanout-lines">
                ${agents.map(() => '<div class="af-wf-fanout-line"></div>').join('')}
            </div>
            <div class="af-wf-arrow-down"><i class="bi bi-arrow-down"></i></div>
            ${buildNodeHtml('Synthesizer', 'synthesizer')}
        </div>
    `;
}

function renderIterativeFlow(agents) {
    const writer = agents[0] || 'Writer';
    const reviewer = agents[1] || 'Reviewer';
    return `
        ${buildNodeHtml(writer)}
        <div class="af-wf-arrow"><i class="bi bi-arrow-right"></i></div>
        ${buildNodeHtml(reviewer)}
        <div class="af-wf-arrow"><i class="bi bi-arrow-left"></i></div>
        <div class="af-wf-loop-label">Loop until approved</div>
    `;
}

function renderGroupChatFlow(agents) {
    return agents.map(a => buildNodeHtml(a)).join('') +
        '<div class="af-wf-groupchat-label"><i class="bi bi-arrow-repeat"></i> Round-robin turns</div>';
}

function renderConcurrentFlow(agents) {
    return `
        <div class="af-wf-fanout-parallel">
            ${agents.map(a => buildNodeHtml(a)).join('')}
        </div>
        <div class="af-wf-groupchat-label"><i class="bi bi-diagram-3"></i> Parallel execution — results aggregated</div>
    `;
}

function renderHandoffFlow(agents) {
    const triage = agents[0];
    const specialists = agents.slice(1);
    return `
        <div class="af-wf-cond-explain-bar">
            <i class="bi bi-robot"></i>
            <span>The <strong>Triage</strong> agent reads the message and <strong>automatically hands off</strong> to the right specialist — no rules needed</span>
        </div>
        <div class="af-wf-cond-how-box">
            <div class="af-wf-cond-how-title"><i class="bi bi-gear"></i> How it works</div>
            <ol class="af-wf-cond-how-steps">
                <li>User sends a message</li>
                <li><strong>${escapeHtml(triage)}</strong> understands the intent using AI</li>
                <li>AI hands off to the specialist that can best help</li>
                <li>Only that specialist responds</li>
            </ol>
        </div>
        <div class="af-wf-cond-router">
            <div class="af-wf-cond-router-badge"><i class="bi bi-signpost-split"></i> AI Triage</div>
            ${buildNodeHtml(triage)}
        </div>
        <div class="af-wf-arrow-down"><i class="bi bi-arrow-down"></i></div>
        <div class="af-wf-cond-decision-diamond">
            <i class="bi bi-question-diamond-fill"></i>
        </div>
        <div class="af-wf-cond-branches">
            ${specialists.map((a, i) => `
                <div class="af-wf-cond-branch">
                    <div class="af-wf-cond-branch-tag">Specialist ${i + 1}</div>
                    <div class="af-wf-cond-branch-line"></div>
                    ${buildNodeHtml(a)}
                </div>
            `).join('')}
        </div>
        <div class="af-wf-groupchat-label"><i class="bi bi-lightbulb"></i> No manual rules — the triage AI decides who handles each message</div>
    `;
}

function renderGroupChatAIFlow(agents) {
    return `
        <div class="af-wf-groupchat-label"><i class="bi bi-robot"></i> AI Moderator selects next speaker</div>
    ` + agents.map(a => buildNodeHtml(a)).join('') +
        '<div class="af-wf-groupchat-label"><i class="bi bi-stars"></i> AI decides who speaks based on context</div>';
}

function updateWorkflowFlowNode(agentName, status) {
    const nodes = document.querySelectorAll('.af-wf-node');
    nodes.forEach(node => {
        if (node.dataset.wfAgent === agentName) {
            node.classList.remove('idle', 'running', 'completed');
            node.classList.add(status);
        }
    });
}

function updateAgentInfo(name) {
    const agent = state.agents.find(a => a.name === name);
    if (!agent) return;

    document.getElementById('infoAgentName').textContent = agent.name;
    document.getElementById('infoAgentModel').textContent = 'gpt-4o';
    document.getElementById('infoAgentTools').textContent = agent.tools?.length || '0';
    document.getElementById('infoAgentProvider').textContent =
        agent.contextProviders?.length ? agent.contextProviders.join(', ') : '—';
    document.getElementById('infoAgentMiddleware').textContent = '3 (Log/Metrics/Audit)';
}

// ---- Event Listeners ----
function setupEventListeners() {
    // Chat form submission
    document.getElementById('chatForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const input = document.getElementById('messageInput');
        const message = input.value.trim();
        if (!message || state.streaming) return;

        input.value = '';
        await sendMessage(message);
    });

    // Agent filter
    document.getElementById('agentFilter').addEventListener('input', renderAgentList);

    // New session
    document.getElementById('btnNewSession').addEventListener('click', () => {
        saveCurrentSessionToStorage();
        state.sessionId = generateId();
        state.messageCount = 0;
        state.currentMessages = [];
        document.getElementById('chatMessages').innerHTML = `
            <div class="af-welcome text-center py-5">
                <i class="bi bi-chat-dots" style="font-size:3rem;color:var(--af-accent);"></i>
                <h4 class="mt-3">New Session</h4>
                <p class="text-secondary">${getBrandingText('welcomeSubtitle')}</p>
            </div>`;
        updateSessionBadge();
        updateAgentContext();
        renderExamplePrompts();
        renderSessionHistory();
    });

    // File attachment (inline, not modal)
    const documentInput = document.getElementById('documentInput');
    const btnAttach = document.getElementById('btnAttach');
    const removeAttachmentBtn = document.getElementById('removeAttachmentBtn');

    btnAttach?.addEventListener('click', () => documentInput?.click());
    documentInput?.addEventListener('change', () => {
        const file = documentInput.files[0];
        if (file) {
            state.attachedDocument = file;
            document.getElementById('attachedFileName').textContent = file.name;
            document.getElementById('attachedFileInfo').style.display = 'flex';
        }
    });
    removeAttachmentBtn?.addEventListener('click', () => {
        state.attachedDocument = null;
        documentInput.value = '';
        document.getElementById('attachedFileInfo').style.display = 'none';
    });

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        if (e.key === '/' && document.activeElement !== document.getElementById('messageInput')) {
            e.preventDefault();
            document.getElementById('messageInput').focus();
        }
    });
}

// ---- Send Message with SSE Streaming ----
async function sendMessage(message) {
    removeWelcome();
    hideAgentContextAndPrompts();
    appendUserMessage(message);
    state.currentMessages.push({ role: 'user', text: message, timestamp: new Date().toISOString() });
    state.streaming = true;
    document.getElementById('btnSend').disabled = true;

    const isMultiAgent = !!(state.currentOrchestration || state.currentWorkflow);

    // Stream context — tracks current agent bubble
    const stream = {
        agentName: state.currentAgent,
        agentMsgEl: null,
        contentEl: null,
        fullText: '',
        responses: []   // collected { agentName, text } for history
    };

    // For single-agent mode, create the bubble immediately
    if (!isMultiAgent) {
        const agent = state.agents.find(a => a.name === state.currentAgent);
        stream.agentMsgEl = appendAgentMessage(state.currentAgent, agent);
        stream.contentEl = stream.agentMsgEl.querySelector('.af-msg-content');
        stream.contentEl.classList.add('af-streaming');
    }

    // Clear attachment after sending
    const attachedFile = state.attachedDocument;
    if (attachedFile) {
        state.attachedDocument = null;
        const docInput = document.getElementById('documentInput');
        if (docInput) docInput.value = '';
        document.getElementById('attachedFileInfo').style.display = 'none';
    }

    try {
        let fetchOptions;
        if (attachedFile) {
            // Send as FormData when file is attached
            const formData = new FormData();
            formData.append('sessionId', state.sessionId);
            formData.append('message', message);
            formData.append('agentName', state.currentAgent);
            if (state.currentOrchestration) {
                formData.append('orchestrationName', state.currentOrchestration);
            } else if (state.currentWorkflow && state.currentWorkflow !== '__custom__') {
                formData.append('workflowName', state.currentWorkflow);
            }
            if (state.currentWorkflow === '__custom__' && state._customWorkflow) {
                state._customWorkflow.agents.forEach(a => formData.append('customAgents', a));
                formData.append('customPattern', state._customWorkflow.pattern);
            }
            formData.append('document', attachedFile);
            fetchOptions = { method: 'POST', body: formData };
        } else {
            const payload = {
                sessionId: state.sessionId,
                message: message,
                agentName: state.currentAgent,
            };
            if (state.currentOrchestration) {
                payload.orchestrationName = state.currentOrchestration;
            } else if (state.currentWorkflow && state.currentWorkflow !== '__custom__') {
                payload.workflowName = state.currentWorkflow;
            }
            if (state.currentWorkflow === '__custom__' && state._customWorkflow) {
                payload.customAgents = state._customWorkflow.agents;
                payload.customPattern = state._customWorkflow.pattern;
            }
            fetchOptions = {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            };
        }

        const response = await fetch('/api/chat/stream', fetchOptions);

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const events = parseSSE(buffer);
            buffer = events.remaining;

            for (const evt of events.parsed) {
                handleStreamEvent(evt, stream);
            }
        }
    } catch (e) {
        console.error('Streaming error:', e);
        if (stream.contentEl) {
            stream.contentEl.innerHTML = '<span class="text-danger">Error: Failed to connect to the server.</span>';
        }
    }

    // Finalize last bubble
    finalizeStreamBubble(stream);

    // Save all agent responses to history
    if (stream.responses.length > 0) {
        for (const r of stream.responses) {
            state.currentMessages.push({ role: 'agent', agentName: r.agentName, text: r.text, timestamp: new Date().toISOString() });
        }
        saveCurrentSessionToStorage();
    }

    state.streaming = false;
    state.messageCount++;
    document.getElementById('btnSend').disabled = false;
    document.getElementById('infoMsgCount').textContent = state.messageCount;
    scrollToBottom();
}

function finalizeStreamBubble(stream) {
    if (stream.contentEl) {
        stream.contentEl.classList.remove('af-streaming');
        if (stream.fullText) {
            stream.contentEl.innerHTML = renderMarkdown(stream.fullText);
            stream.responses.push({ agentName: stream.agentName, text: stream.fullText });
        }
    }
}

function switchStreamAgent(stream, newAgentName) {
    // Finalize the previous bubble
    finalizeStreamBubble(stream);

    // Create new bubble for the new agent
    stream.agentName = newAgentName;
    const agentInfo = state.agents.find(a => a.name === newAgentName);
    stream.agentMsgEl = appendAgentMessage(newAgentName, agentInfo);
    stream.contentEl = stream.agentMsgEl.querySelector('.af-msg-content');
    stream.contentEl.classList.add('af-streaming');
    stream.fullText = '';
}

function handleStreamEvent(evt, stream) {
    switch (evt.type) {
        case 'agent-start':
            switchStreamAgent(stream, evt.data?.agentName || 'Agent');
            updateInlineFlowProgress(evt.data?.agentName || 'Agent', 'running');
            break;

        case 'agent-token':
            if (evt.data?.text) {
                stream.fullText += evt.data.text;
                if (stream.contentEl) {
                    throttledRenderStreaming(stream.contentEl, stream.fullText);
                }
            }
            scrollToBottom();
            break;

        case 'tool-call':
            if (stream.agentMsgEl) {
                const toolCard = createToolCard(evt.data);
                stream.agentMsgEl.appendChild(toolCard);
            }
            addExecutionStep(evt.data?.data?.toolName || 'Tool', 'running');
            break;

        case 'tool-result':
            updateToolCard(evt.data);
            addExecutionStep(evt.data?.data?.toolName || 'Tool', 'completed');
            break;

        case 'tool-approval':
            showApprovalDialog(evt.data);
            break;

        case 'agent-complete':
            addExecutionStep(evt.data?.agentName || state.currentAgent, 'completed');
            updateInlineFlowProgress(evt.data?.agentName || state.currentAgent, 'completed');
            break;

        case 'workflow-step':
            const wfStepName = evt.data?.data?.executorName || 'Step';
            const wfStepStatus = evt.data?.data?.status || 'running';
            addExecutionStep(wfStepName, wfStepStatus);
            updateWorkflowFlowNode(wfStepName, wfStepStatus);
            updateInlineFlowProgress(wfStepName, wfStepStatus);
            break;

        case 'error':
            if (stream.contentEl) {
                stream.contentEl.innerHTML += `<span class="text-danger">${escapeHtml(evt.data?.text || 'Unknown error')}</span>`;
            }
            break;

        case 'done':
            break;
    }
}

// ---- SSE Parser ----
function parseSSE(text) {
    const events = [];
    const lines = text.split('\n');
    let remaining = '';
    let currentEvent = {};

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (line.startsWith('event: ')) {
            currentEvent.type = line.substring(7).trim();
        } else if (line.startsWith('data: ')) {
            try {
                currentEvent.data = JSON.parse(line.substring(6));
            } catch {
                currentEvent.data = { text: line.substring(6) };
            }
        } else if (line === '' && currentEvent.type) {
            events.push({ ...currentEvent });
            currentEvent = {};
        } else if (i === lines.length - 1 && line !== '') {
            remaining = line;
        }
    }

    // If we have an incomplete event at the end
    if (currentEvent.type && currentEvent.data) {
        events.push({ ...currentEvent });
    }

    return { parsed: events, remaining };
}

// ---- DOM Helpers ----
function removeWelcome() {
    const welcome = document.querySelector('.af-welcome');
    if (welcome) welcome.remove();
}

function appendUserMessage(text) {
    const el = document.createElement('div');
    el.className = 'af-msg-with-avatar af-msg-user';
    el.innerHTML = `
        <div class="af-msg-avatar" style="background:var(--af-accent);"><i class="bi bi-person-fill" style="font-size:0.8rem;"></i></div>
        <div class="af-msg-bubble">${escapeHtml(text)}</div>
    `;
    document.getElementById('chatMessages').appendChild(el);
    scrollToBottom();
}

function appendAgentMessage(agentName, agent) {
    const color = agent?.color || '#0078d4';
    const icon = agent?.icon || '\uD83E\uDD16';
    const el = document.createElement('div');
    el.className = 'af-msg-with-avatar af-msg-agent';
    el.innerHTML = `
        <div class="af-msg-avatar" style="background:${color};">${icon}</div>
        <div class="af-msg-bubble">
            <div class="af-msg-header">${escapeHtml(agentName)}</div>
            <div class="af-msg-content"></div>
        </div>
    `;
    document.getElementById('chatMessages').appendChild(el);
    scrollToBottom();
    return el;
}

function createToolCard(data) {
    const card = document.createElement('div');
    card.className = 'af-tool-card';
    const toolName = data?.data?.toolName || data?.toolName || 'Tool';
    const args = data?.data?.args || data?.args || {};
    card.innerHTML = `
        <div class="af-tool-header" onclick="this.parentElement.classList.toggle('expanded')">
            <i class="bi bi-wrench"></i>
            <span>${escapeHtml(toolName)}</span>
            <i class="bi bi-chevron-down ms-auto"></i>
        </div>
        <div class="af-tool-body">
            <div class="text-secondary small">Arguments:</div>
            <pre>${escapeHtml(JSON.stringify(args, null, 2))}</pre>
            <div class="text-secondary small af-tool-result-label" style="display:none;">Result:</div>
            <pre class="af-tool-result" style="display:none;"></pre>
        </div>
    `;
    return card;
}

function updateToolCard(data) {
    const cards = document.querySelectorAll('.af-tool-card');
    const lastCard = cards[cards.length - 1];
    if (lastCard) {
        const result = data?.data?.result || data?.result || '';
        const resultEl = lastCard.querySelector('.af-tool-result');
        const labelEl = lastCard.querySelector('.af-tool-result-label');
        if (resultEl && labelEl) {
            resultEl.textContent = typeof result === 'string' ? result : JSON.stringify(result, null, 2);
            resultEl.style.display = 'block';
            labelEl.style.display = 'block';
        }
    }
}

// ---- Execution Flow ----
function addExecutionStep(name, status) {
    const flow = document.getElementById('executionFlow');
    if (flow.querySelector('.text-secondary')) flow.innerHTML = '';

    const icon = status === 'completed' ? 'bi-check-circle-fill text-success'
        : status === 'running' ? 'bi-arrow-right-circle text-info'
        : 'bi-hourglass-split text-warning';

    // Add connector line if not first
    if (flow.children.length > 0) {
        const line = document.createElement('div');
        line.className = 'af-exec-line';
        flow.appendChild(line);
    }

    const step = document.createElement('div');
    step.className = 'af-exec-step';
    step.innerHTML = `<i class="bi ${icon}"></i><span>${escapeHtml(name)}</span>`;
    flow.appendChild(step);
}

// ---- Approval Dialog ----
function showApprovalDialog(data) {
    const toolName = data?.data?.toolName || data?.toolName || 'Unknown tool';
    const args = data?.data?.args || data?.args || {};
    const requestId = data?.data?.requestId || '';

    document.getElementById('approvalToolName').textContent = `Tool: ${toolName}`;
    document.getElementById('approvalArgs').textContent = JSON.stringify(args, null, 2);

    const modal = new bootstrap.Modal(document.getElementById('approvalModal'));

    document.getElementById('btnApprove').onclick = () => {
        sendApproval(requestId, true);
        modal.hide();
    };
    document.getElementById('btnReject').onclick = () => {
        sendApproval(requestId, false);
        modal.hide();
    };

    modal.show();
}

async function sendApproval(requestId, approved) {
    try {
        await fetch('/api/chat/approve', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ requestId, approved })
        });
    } catch (e) {
        console.error('Failed to send approval:', e);
    }
}

// ---- Document Upload ----
async function uploadFiles(files) {
    for (const file of files) {
        const formData = new FormData();
        formData.append('file', file);
        try {
            await fetch('/api/document/upload', { method: 'POST', body: formData });
        } catch (e) {
            console.error('Upload error:', e);
        }
    }
}

// ---- Agent Context Header ----
function updateAgentContext() {
    const ctx = document.getElementById('agentContext');
    if (!ctx) return;

    const agent = state.agents.find(a => a.name === state.currentAgent);
    if (!agent) {
        ctx.style.display = 'none';
        return;
    }

    ctx.style.display = 'flex';
    document.getElementById('agentContextIcon').innerHTML = agent.icon;
    document.getElementById('agentContextIcon').style.backgroundColor = agent.color || '#0078d4';
    document.getElementById('agentContextName').textContent = agent.name;
    document.getElementById('agentContextDesc').textContent = agent.description;

    const toolsEl = document.getElementById('agentContextTools');
    if (agent.tools && agent.tools.length > 0) {
        toolsEl.innerHTML = agent.tools.map(t => `<span class="tool-badge">${escapeHtml(t)}</span>`).join('');
    } else {
        toolsEl.innerHTML = '';
    }
}

function renderExamplePrompts() {
    const el = document.getElementById('examplePrompts');
    if (!el) return;

    const agent = state.agents.find(a => a.name === state.currentAgent);
    const prompts = agent?.examplePrompts || [];

    if (prompts.length === 0) {
        el.style.display = 'none';
        return;
    }

    el.style.display = 'flex';
    el.innerHTML = prompts.slice(0, 3).map(p => `
        <button class="af-prompt-chip" onclick="handlePromptClick(this)">${escapeHtml(p)}</button>
    `).join('');
}

function handlePromptClick(btn) {
    const input = document.getElementById('messageInput');
    input.value = btn.textContent.trim();
    input.focus();
}

function hideAgentContextAndPrompts() {
    const ctx = document.getElementById('agentContext');
    const prompts = document.getElementById('examplePrompts');
    if (ctx) ctx.style.display = 'none';
    if (prompts) prompts.style.display = 'none';
}

// ---- Utilities ----
function renderMarkdown(text) {
    if (typeof marked !== 'undefined') {
        let html = marked.parse(text);
        // Sanitize with DOMPurify if available
        if (typeof DOMPurify !== 'undefined') {
            html = DOMPurify.sanitize(html, { ADD_ATTR: ['target'] });
        }
        // Add copy buttons to code blocks
        html = html.replace(/<pre><code(.*?)>/g, (match, attrs) => {
            return `<pre><button class="af-copy-btn" onclick="copyCodeBlock(this)"><i class="bi bi-clipboard"></i> Copy</button><code${attrs}>`;
        });
        // Open links in new tab
        html = html.replace(/<a /g, '<a target="_blank" rel="noopener noreferrer" ');
        return html;
    }
    return escapeHtml(text).replace(/\n/g, '<br>');
}

function copyCodeBlock(btn) {
    const pre = btn.closest('pre');
    const code = pre.querySelector('code');
    if (!code) return;
    navigator.clipboard.writeText(code.textContent).then(() => {
        btn.innerHTML = '<i class="bi bi-check2"></i> Copied!';
        setTimeout(() => { btn.innerHTML = '<i class="bi bi-clipboard"></i> Copy'; }, 2000);
    }).catch(() => {
        btn.textContent = 'Failed';
        setTimeout(() => { btn.innerHTML = '<i class="bi bi-clipboard"></i> Copy'; }, 2000);
    });
}

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function scrollToBottom() {
    const container = document.getElementById('chatMessages');
    container.scrollTop = container.scrollHeight;
}

function updateSessionBadge() {
    const badge = document.getElementById('sessionBadge');
    if (badge) badge.textContent = `Session: ${state.sessionId.substring(0, 8)}...`;
    document.getElementById('infoSessionId').textContent = state.sessionId.substring(0, 12) + '...';
}

function generateId() {
    return crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2) + Date.now().toString(36);
}

function getBrandingText(key) {
    const wrapper = document.querySelector('.chat-page-wrapper');
    const defaults = {
        welcomeTitle: 'Welcome to AF-WebChat',
        welcomeSubtitle: 'Select an agent and start chatting.'
    };
    if (!wrapper) return defaults[key] || '';
    return wrapper.dataset[key] || defaults[key] || '';
}

// ---- Streaming Markdown Rendering ----
let _lastStreamRender = 0;
const STREAM_RENDER_INTERVAL = 50;

function renderStreamingMarkdown(text) {
    // Close unclosed code fences for clean partial rendering
    const fenceCount = (text.match(/```/g) || []).length;
    if (fenceCount % 2 !== 0) {
        text += '\n```';
    }
    return renderMarkdown(text);
}

function throttledRenderStreaming(contentEl, fullText) {
    const now = Date.now();
    if (now - _lastStreamRender >= STREAM_RENDER_INTERVAL) {
        contentEl.innerHTML = renderStreamingMarkdown(fullText);
        _lastStreamRender = now;
    }
}

// ---- Session History (localStorage) ----
const STORAGE_SESSIONS_KEY = 'af-webchat-sessions';
const STORAGE_MESSAGES_PREFIX = 'af-webchat-messages-';

function loadSessionHistory() {
    renderSessionHistory();
}

function getSessionsFromStorage() {
    try {
        return JSON.parse(localStorage.getItem(STORAGE_SESSIONS_KEY) || '[]');
    } catch { return []; }
}

function saveSessionsToStorage(sessions) {
    localStorage.setItem(STORAGE_SESSIONS_KEY, JSON.stringify(sessions));
}

function getMessagesFromStorage(sessionId) {
    try {
        return JSON.parse(localStorage.getItem(STORAGE_MESSAGES_PREFIX + sessionId) || '[]');
    } catch { return []; }
}

function saveMessagesToStorage(sessionId, messages) {
    localStorage.setItem(STORAGE_MESSAGES_PREFIX + sessionId, JSON.stringify(messages));
}

function saveCurrentSessionToStorage() {
    if (state.currentMessages.length === 0) return;

    const sessions = getSessionsFromStorage();
    const existingIdx = sessions.findIndex(s => s.id === state.sessionId);
    const firstUserMsg = state.currentMessages.find(m => m.role === 'user');
    const title = firstUserMsg ? firstUserMsg.text.substring(0, 80) : 'New session';

    const sessionEntry = {
        id: state.sessionId,
        agentName: state.currentAgent,
        title: title,
        createdAt: existingIdx >= 0 ? sessions[existingIdx].createdAt : new Date().toISOString(),
        lastActivity: new Date().toISOString(),
        messageCount: state.currentMessages.length
    };

    if (existingIdx >= 0) {
        sessions[existingIdx] = sessionEntry;
    } else {
        sessions.unshift(sessionEntry);
    }

    // Keep max 50 sessions
    if (sessions.length > 50) {
        const removed = sessions.splice(50);
        removed.forEach(s => localStorage.removeItem(STORAGE_MESSAGES_PREFIX + s.id));
    }

    saveSessionsToStorage(sessions);
    saveMessagesToStorage(state.sessionId, state.currentMessages);
    renderSessionHistory();
}

function switchToSession(sessionId) {
    if (sessionId === state.sessionId) return;

    // Save current session first
    saveCurrentSessionToStorage();

    const sessions = getSessionsFromStorage();
    const session = sessions.find(s => s.id === sessionId);
    if (!session) return;

    // Update state
    state.sessionId = sessionId;
    state.currentAgent = session.agentName;
    state.currentMessages = getMessagesFromStorage(sessionId);
    state.messageCount = state.currentMessages.length;

    // Update agent selection
    renderAgentList();
    updateAgentInfo(state.currentAgent);
    updateSessionBadge();

    // Hide workflow flow, show agent context
    const flowEl = document.getElementById('workflowFlow');
    if (flowEl) flowEl.style.display = 'none';

    // Clear and re-render messages
    const chatEl = document.getElementById('chatMessages');
    chatEl.innerHTML = '';

    if (state.currentMessages.length === 0) {
        chatEl.innerHTML = `
            <div class="af-welcome text-center py-5">
                <i class="bi bi-chat-dots" style="font-size:3rem;color:var(--af-accent);"></i>
                <h4 class="mt-3">${getBrandingText('welcomeTitle')}</h4>
                <p class="text-secondary">${getBrandingText('welcomeSubtitle')}</p>
            </div>`;
    } else {
        for (const msg of state.currentMessages) {
            if (msg.role === 'user') {
                appendUserMessage(msg.text);
            } else if (msg.role === 'agent') {
                const agent = state.agents.find(a => a.name === msg.agentName);
                const agentMsgEl = appendAgentMessage(msg.agentName || state.currentAgent, agent);
                const contentEl = agentMsgEl.querySelector('.af-msg-content');
                contentEl.innerHTML = renderMarkdown(msg.text);
            }
        }
    }

    document.getElementById('infoMsgCount').textContent = state.messageCount;

    // Close drawer
    const drawerEl = document.getElementById('sessionDrawer');
    if (drawerEl) {
        const drawer = bootstrap.Offcanvas.getInstance(drawerEl);
        if (drawer) drawer.hide();
    }

    renderSessionHistory();
    scrollToBottom();
}

function deleteSessionFromStorage(sessionId) {
    if (sessionId === state.sessionId) return;

    let sessions = getSessionsFromStorage();
    sessions = sessions.filter(s => s.id !== sessionId);
    saveSessionsToStorage(sessions);
    localStorage.removeItem(STORAGE_MESSAGES_PREFIX + sessionId);
    renderSessionHistory();
}

function renderSessionHistory() {
    const list = document.getElementById('sessionHistoryList');
    if (!list) return;

    const sessions = getSessionsFromStorage();

    if (sessions.length === 0) {
        list.innerHTML = `
            <div class="af-session-empty">
                <i class="bi bi-chat-square-text"></i>
                <p>No conversation history</p>
            </div>`;
        return;
    }

    list.innerHTML = sessions.map(s => {
        const isActive = s.id === state.sessionId;
        const timeAgo = getRelativeTime(s.lastActivity);
        const agent = state.agents.find(a => a.name === s.agentName);
        const icon = agent?.icon || '\uD83E\uDD16';
        const color = agent?.color || '#0078d4';

        return `
        <div class="af-session-item ${isActive ? 'active' : ''}" data-sid="${escapeHtml(s.id)}" onclick="switchToSession(this.dataset.sid)">
            <div class="af-session-item-icon" style="background:${color}">${icon}</div>
            <div class="af-session-item-info">
                <div class="af-session-item-title">${escapeHtml(s.title)}</div>
                <div class="af-session-item-meta">
                    <span>${escapeHtml(s.agentName)}</span>
                    <span>\u00B7</span>
                    <span>${timeAgo}</span>
                    <span>\u00B7</span>
                    <span>${s.messageCount} msgs</span>
                </div>
            </div>
            ${!isActive ? `<button class="af-session-item-delete" onclick="event.stopPropagation(); deleteSessionFromStorage('${escapeHtml(s.id)}')" title="Delete">
                <i class="bi bi-trash3"></i>
            </button>` : ''}
        </div>`;
    }).join('');
}

function toggleSessionDrawer() {
    const el = document.getElementById('sessionDrawer');
    if (!el) return;
    const drawer = bootstrap.Offcanvas.getOrCreateInstance(el);
    drawer.toggle();
}

function getRelativeTime(dateStr) {
    const now = new Date();
    const date = new Date(dateStr);
    const diff = Math.floor((now - date) / 1000);

    if (diff < 60) return 'just now';
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    if (diff < 604800) return `${Math.floor(diff / 86400)}d ago`;
    return date.toLocaleDateString();
}

// =============================================
// Agent Studio — Create Agent & Design Flow
// =============================================

const studio = {
    selectedIcon: '🤖',
    selectedColor: '#0078d4',
    availableTools: [],
    selectedTools: new Set(),
    creating: false
};

// ---- Studio Tab Switching ----
function switchStudioTab(tab) {
    document.querySelectorAll('.af-studio-tab').forEach(t => t.classList.toggle('active', t.dataset.studio === tab));
    document.getElementById('studioCreate').style.display = tab === 'create' ? '' : 'none';
    document.getElementById('studioFlow').style.display = tab === 'flow' ? '' : 'none';
    if (tab === 'create') loadStudioTools();
    if (tab === 'flow') renderBuilderAgentPicker();
}

// ---- Icon & Color Pickers ----
function selectStudioIcon(btn) {
    document.querySelectorAll('#studioIconPicker .af-icon-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    studio.selectedIcon = btn.dataset.icon;
    updateStudioPreview();
}

function selectStudioColor(btn) {
    document.querySelectorAll('#studioColorPicker .af-color-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    studio.selectedColor = btn.dataset.color;
    updateStudioPreview();
}

// ---- Preview Card Update ----
function updateStudioPreview() {
    const name = document.getElementById('studioAgentName')?.value || 'New Agent';
    document.getElementById('studioPreviewIcon').textContent = studio.selectedIcon;
    document.getElementById('studioPreviewIcon').style.backgroundColor = studio.selectedColor;
    document.getElementById('studioPreviewName').textContent = name || 'New Agent';
}

// ---- Load Available Tools ----
async function loadStudioTools() {
    if (studio.availableTools.length > 0) {
        renderStudioTools();
        return;
    }
    try {
        const res = await fetch('/api/agent/tools');
        studio.availableTools = await res.json();
        renderStudioTools();
    } catch (e) {
        console.error('Failed to load tools:', e);
        document.getElementById('studioToolList').innerHTML = '<span class="text-secondary" style="font-size:0.7rem;">No tools available</span>';
    }
}

function renderStudioTools() {
    const el = document.getElementById('studioToolList');
    if (!el) return;

    if (studio.availableTools.length === 0) {
        el.innerHTML = '<span class="text-secondary" style="font-size:0.7rem;">No tools registered</span>';
        return;
    }

    el.innerHTML = studio.availableTools.map(t => {
        const checked = studio.selectedTools.has(t) ? 'checked' : '';
        return `<label class="af-studio-tool-item">
            <input type="checkbox" ${checked} onchange="toggleStudioTool('${escapeHtml(t)}', this.checked)" />
            <i class="bi bi-wrench"></i>
            <span>${escapeHtml(t)}</span>
        </label>`;
    }).join('');
}

function toggleStudioTool(name, checked) {
    if (checked) studio.selectedTools.add(name);
    else studio.selectedTools.delete(name);
}

// ---- Example Prompts ----
function addStudioPrompt() {
    const container = document.getElementById('studioExamplePrompts');
    const row = document.createElement('div');
    row.className = 'af-studio-prompt-row';
    row.innerHTML = `
        <input type="text" class="form-control form-control-sm af-input" placeholder="Example prompt..." />
        <button class="af-studio-prompt-remove" onclick="removeStudioPrompt(this)"><i class="bi bi-x"></i></button>
    `;
    container.appendChild(row);
}

function removeStudioPrompt(btn) {
    const row = btn.closest('.af-studio-prompt-row');
    const container = document.getElementById('studioExamplePrompts');
    if (container.children.length > 1) {
        row.remove();
    } else {
        row.querySelector('input').value = '';
    }
}

// ---- Instructions char counter ----
document.addEventListener('DOMContentLoaded', () => {
    const instrEl = document.getElementById('studioInstructions');
    if (instrEl) {
        instrEl.addEventListener('input', () => {
            document.getElementById('studioInstructionsCount').textContent = instrEl.value.length;
        });
    }
});

// ---- Create Agent ----
async function createStudioAgent() {
    if (studio.creating) return;

    const name = document.getElementById('studioAgentName')?.value?.trim();
    const desc = document.getElementById('studioAgentDesc')?.value?.trim();
    const instructions = document.getElementById('studioInstructions')?.value?.trim();

    if (!name) {
        showStudioToast('Please enter an agent name.', 'warning');
        return;
    }
    if (!instructions) {
        showStudioToast('Please enter instructions (system prompt).', 'warning');
        return;
    }

    const prompts = Array.from(document.querySelectorAll('#studioExamplePrompts input'))
        .map(e => e.value.trim())
        .filter(Boolean);

    const btn = document.getElementById('btnCreateAgent');
    studio.creating = true;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Creating...';

    try {
        const res = await fetch('/api/agent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name,
                description: desc || `Custom agent: ${name}`,
                icon: studio.selectedIcon,
                color: studio.selectedColor,
                instructions,
                tools: Array.from(studio.selectedTools),
                examplePrompts: prompts
            })
        });

        if (!res.ok) {
            const errText = await res.text();
            showStudioToast(errText || 'Failed to create agent.', 'error');
            return;
        }

        const agentInfo = await res.json();

        // Reload agents and select the new one
        await loadAgents();
        selectAgent(agentInfo.name);
        switchSidebarTab('agents');

        showStudioToast(`Agent "${agentInfo.name}" created! 🚀`, 'success');
        resetStudioForm();
    } catch (e) {
        console.error('Failed to create agent:', e);
        showStudioToast('Network error creating agent.', 'error');
    } finally {
        studio.creating = false;
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-rocket-takeoff"></i> Create & Use';
    }
}

// ---- Reset Form ----
function resetStudioForm() {
    document.getElementById('studioAgentName').value = '';
    document.getElementById('studioAgentDesc').value = '';
    document.getElementById('studioInstructions').value = '';
    document.getElementById('studioInstructionsCount').textContent = '0';
    studio.selectedIcon = '🤖';
    studio.selectedColor = '#0078d4';
    studio.selectedTools.clear();

    document.querySelectorAll('#studioIconPicker .af-icon-btn').forEach(b => b.classList.toggle('active', b.dataset.icon === '🤖'));
    document.querySelectorAll('#studioColorPicker .af-color-btn').forEach(b => b.classList.toggle('active', b.dataset.color === '#0078d4'));

    document.getElementById('studioExamplePrompts').innerHTML = `
        <div class="af-studio-prompt-row">
            <input type="text" class="form-control form-control-sm af-input" placeholder="Example prompt 1..." />
            <button class="af-studio-prompt-remove" onclick="removeStudioPrompt(this)"><i class="bi bi-x"></i></button>
        </div>`;

    renderStudioTools();
    updateStudioPreview();
}

// ---- Toast notification ----
function showStudioToast(message, type) {
    const existing = document.querySelector('.af-studio-toast');
    if (existing) existing.remove();

    const colors = { success: '#238636', error: '#f85149', warning: '#e09b13' };
    const icons = { success: 'bi-check-circle-fill', error: 'bi-exclamation-circle-fill', warning: 'bi-exclamation-triangle-fill' };

    const toast = document.createElement('div');
    toast.className = 'af-studio-toast';
    toast.style.background = colors[type] || colors.success;
    toast.innerHTML = `<i class="bi ${icons[type] || icons.success}"></i> ${escapeHtml(message)}`;
    document.body.appendChild(toast);

    requestAnimationFrame(() => toast.classList.add('show'));
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// =============================================
// Flow Progress Tracking (real-time)
// =============================================

const flowProgress = {
    steps: {},    // { agentName: 'idle' | 'running' | 'completed' }
    active: false
};

function showFlowProgress() {
    const tracker = document.getElementById('flowProgressTracker');
    if (tracker) tracker.style.display = '';
    flowProgress.active = true;

    // Initialize all steps as idle
    flowProgress.steps = {};
    state.builderAgents.forEach(name => {
        flowProgress.steps[name] = 'idle';
    });

    renderFlowTimeline();
    updateFlowProgressBar();
}

function hideFlowProgress() {
    const tracker = document.getElementById('flowProgressTracker');
    if (tracker) tracker.style.display = 'none';
    flowProgress.active = false;
    flowProgress.steps = {};
}

function updateInlineFlowProgress(agentName, status) {
    // Show the tracker if not visible
    if (!flowProgress.active && state.builderAgents.length > 0) {
        showFlowProgress();
    }

    // Update step status
    if (flowProgress.steps.hasOwnProperty(agentName)) {
        flowProgress.steps[agentName] = status;
    } else {
        flowProgress.steps[agentName] = status;
    }

    // Update inline flow canvas nodes
    const nodes = document.querySelectorAll('.af-iflow-node');
    nodes.forEach(node => {
        if (node.dataset.flowAgent === agentName) {
            node.classList.remove('idle', 'running', 'completed');
            node.classList.add(status);
        }
    });

    // Update pipeline steps in sidebar
    const steps = document.querySelectorAll('.af-flow-step');
    steps.forEach(step => {
        const idx = parseInt(step.dataset.idx);
        const stepAgentName = state.builderAgents[idx];
        if (stepAgentName === agentName) {
            step.classList.remove('idle', 'running', 'completed');
            step.classList.add(status);
        }
    });

    renderFlowTimeline();
    updateFlowProgressBar();
}

function renderFlowTimeline() {
    const el = document.getElementById('flowStepsTimeline');
    if (!el) return;

    const entries = Object.entries(flowProgress.steps);
    el.innerHTML = entries.map(([name, status], i) => {
        const a = state.agents.find(x => x.name === name);
        const icon = a?.icon || '\uD83E\uDD16';
        const color = a?.color || '#0078d4';
        const statusIcon = status === 'completed' ? 'bi-check-circle-fill'
            : status === 'running' ? 'bi-arrow-right-circle-fill'
            : 'bi-circle';
        const statusColor = status === 'completed' ? 'var(--af-success)'
            : status === 'running' ? 'var(--af-accent)'
            : 'var(--af-text-dim)';
        return `<div class="af-flow-timeline-step ${status}">
            <div class="af-flow-timeline-indicator" style="color:${statusColor}">
                <i class="bi ${statusIcon}"></i>
            </div>
            <div class="af-flow-timeline-icon" style="background:${color}">${icon}</div>
            <div class="af-flow-timeline-info">
                <span class="af-flow-timeline-name">${escapeHtml(name)}</span>
                <span class="af-flow-timeline-status">${status}</span>
            </div>
        </div>`;
    }).join('');
}

function updateFlowProgressBar() {
    const fill = document.getElementById('flowProgressFill');
    if (!fill) return;

    const entries = Object.values(flowProgress.steps);
    const total = entries.length;
    if (total === 0) { fill.style.width = '0%'; return; }

    const completed = entries.filter(s => s === 'completed').length;
    const running = entries.filter(s => s === 'running').length;
    const pct = Math.round(((completed + running * 0.5) / total) * 100);
    fill.style.width = pct + '%';

    // Color: green when all done, blue otherwise
    if (completed === total) {
        fill.style.background = 'var(--af-success)';
    } else {
        fill.style.background = 'linear-gradient(90deg, var(--af-accent), var(--af-accent-hover))';
    }
}
