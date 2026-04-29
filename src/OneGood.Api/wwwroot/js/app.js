// OneGood - Static Frontend
(function() {
'use strict';
const API_BASE = '';
const STORAGE_KEY_SEEN = 'onegood_seen_causes';
const STORAGE_KEY_ACTED = 'onegood_acted_causes';
const STORAGE_KEY_STREAK = 'onegood_streak';
const STORAGE_KEY_TODAY = 'onegood_today_completion';

// ============ i18n (Internationalization) ============
const translations = {
    en: {
        loading: "Loading today's action...",
        theStory: "The Story",
        aiSummary: "✨ AI summary",
        yourImpact: "Your Impact",
        skip: "Skip",
        opened: "Opened ↗",
        openedMessage: "The cause page has been opened. Take action there if you can!",
        showNext: "Next cause",
        browseMore: "Browse more causes →",
        connectionError: "Connection Error",
        retry: "Retry",
        allDone: "All done!",
        noMoreActions: "No more actions available right now.",
        noMoreInCategory: "No more in this category",
        tryAnotherCategory: "Try another category or browse all causes.",
        showAllCauses: "Show all causes",
        donate: "Donate",
        sign: "Sign Petition",
        write: "Write Letter",
        share: "Share",
        linkCopied: "Link copied!",
        takeAction: "Take Action",
        causesExplored: "{count} causes explored today",
        causeExplored: "1 cause explored today",
        allCategories: "All",
        filterByCategory: "Filter by category",
        aiGenerated: "AI-generated",
        aiSummary: "AI summary",
        whyNow: "Why Now",
        originalText: "Original text",
        showOriginal: "Show original",
        showAiSummary: "Show AI summary",
        allTypes: "All",
        typeDonate: "Donate",
        typePetitions: "Petitions",
        typeLetters: "Letters",
        completionTitle: "You did your OneGood today!",
        comeBackTomorrow: "Come back tomorrow to keep your streak going.",
        exploreMore: "Explore more causes \u2192",
        streakDay: "Day {count}",
        streakMessage: "You\u2019ve done something good {count} days in a row.",
        streakFirstDay: "Day 1 \u2014 Your streak begins!",
        exploreCauses: "Explore Causes",
        backToToday: "\u2190 Back",
        supportShort: "Support this free service",
        supportCompletionText: "This is a free service — if it helps you, please support me to cover server costs.",
        // Legal
        legalImprint: "Legal",
        legalPrivacy: "Privacy",
        legalExternal: "External",
        legalImprintTitle: "Legal Notice",
        legalImprintIntro: "Information according to § 5 DDG",
        legalImprintName: "Claudio Di Lorenzo",
        legalImprintEmail: "Email: cldilorenzo.cdl@gmail.com",
        legalPrivacyTitle: "Privacy Policy",
        legalPrivacyIntro: "TuWasGutes only processes data required to operate the app.",
        legalPrivacyDataPoints: "This includes in particular:",
        legalPrivacyDataList: "- technical server log data<br>- usage data for completed or skipped actions<br>- streak and progress data (anonymous)<br>- technical connection data for real-time updates",
        legalPrivacyUsage: "The data is used solely to operate and improve TuWasGutes.",
        legalPrivacyAnon: "Anonymous user data is stored for tracking your streak and progress. No personal identification is stored.",
        legalPrivacyContact: "Contact for privacy requests: cldilorenzo.cdl@gmail.com",
        legalExternalTitle: "External Content",
        legalExternalDisclaimer: "TuWasGutes links to external websites and organizations. Their operators are solely responsible for their content, donation processes, and privacy practices.",
        legalExternalNoDonations: "TuWasGutes does not collect donations itself.",
        // Categories
        categories: {
            ClimateAndNature: "Climate & Nature",
            HumanRights: "Human Rights",
            Peace: "Peace",
            Education: "Education",
            CleanWater: "Clean Water",
            FoodSecurity: "Food Security",
            AnimalWelfare: "Animal Welfare",
            MentalHealth: "Health",
            Refugees: "Refugees",
            Democracy: "Democracy"
        }
    },
    de: {
        loading: "Lade heutige Aktion...",
        theStory: "Die Geschichte",
        aiSummary: "✨ KI-Zusammenfassung",
        yourImpact: "Deine Wirkung",
        skip: "Überspringen",
        opened: "Geöffnet ↗",
        openedMessage: "Die Projektseite wurde geöffnet. Werde dort aktiv, wenn du kannst!",
        showNext: "Nächstes Projekt",
        browseMore: "Mehr Projekte entdecken →",
        connectionError: "Verbindungsfehler",
        retry: "Erneut versuchen",
        allDone: "Alles erledigt!",
        noMoreActions: "Momentan keine weiteren Aktionen verfügbar.",
        noMoreInCategory: "Keine weiteren in dieser Kategorie",
        tryAnotherCategory: "Probiere eine andere Kategorie oder alle Projekte.",
        showAllCauses: "Alle Projekte anzeigen",
        donate: "Spenden",
        sign: "Petition unterschreiben",
        write: "Brief schreiben",
        share: "Teilen",
        linkCopied: "Link kopiert!",
        takeAction: "Jetzt handeln",
        causesExplored: "{count} Projekte angesehen heute",
        causeExplored: "1 Projekt angesehen heute",
        allCategories: "Alle",
        filterByCategory: "Nach Kategorie filtern",
        aiGenerated: "KI-generiert",
        aiSummary: "KI-Zusammenfassung",
        whyNow: "Warum jetzt",
        originalText: "Originaltext",
        showOriginal: "Original anzeigen",
        showAiSummary: "KI-Zusammenfassung anzeigen",
        allTypes: "Alle",
        typeDonate: "Spenden",
        typePetitions: "Petitionen",
        typeLetters: "Briefe",
        completionTitle: "Du hast dein OneGood f\u00fcr heute getan!",
        comeBackTomorrow: "Komm morgen wieder, um deine Serie fortzusetzen.",
        exploreMore: "Mehr Projekte entdecken \u2192",
        streakDay: "Tag {count}",
        streakMessage: "Du hast {count} Tage in Folge etwas Gutes getan.",
        streakFirstDay: "Tag 1 \u2014 Deine Serie beginnt!",
        exploreCauses: "Projekte entdecken",
        backToToday: "\u2190 Zur\u00fcck",
        supportShort: "Diesen kostenlosen Service unterstützen",
        supportCompletionText: "Dies ist ein kostenloser Service — wenn er dir hilft, unterstütze mich bitte bei den Serverkosten.",
        // Legal
        legalImprint: "Impressum",
        legalPrivacy: "Datenschutz",
        legalExternal: "Externe Inhalte",
        legalImprintTitle: "Impressum",
        legalImprintIntro: "Angaben gemäß § 5 DDG",
        legalImprintName: "Claudio Di Lorenzo",
        legalImprintEmail: "E-Mail: cldilorenzo.cdl@gmail.com",
        legalPrivacyTitle: "Datenschutz",
        legalPrivacyIntro: "TuWasGutes verarbeitet nur die Daten, die für den Betrieb der App notwendig sind.",
        legalPrivacyDataPoints: "Dazu gehören insbesondere:",
        legalPrivacyDataList: "- technische Server-Logdaten<br>- Nutzungsdaten für abgeschlossene oder übersprungene Aktionen<br>- Streak- und Fortschrittsdaten (anonym)<br>- technische Verbindungsdaten für Echtzeit-Updates",
        legalPrivacyUsage: "Die Daten werden ausschließlich zum Betrieb und zur Verbesserung von TuWasGutes verwendet.",
        legalPrivacyAnon: "Anonyme Nutzerdaten werden zur Nachverfolgung deiner Streak und Fortschritte gespeichert. Es werden keine personenbezogenen Daten gespeichert.",
        legalPrivacyContact: "Kontakt für Datenschutzanfragen: cldilorenzo.cdl@gmail.com",
        legalExternalTitle: "Externe Inhalte",
        legalExternalDisclaimer: "TuWasGutes verlinkt auf externe Webseiten und Organisationen. Für deren Inhalte, Spendenprozesse und Datenschutzpraktiken sind ausschließlich die jeweiligen Betreiber verantwortlich.",
        legalExternalNoDonations: "TuWasGutes sammelt selbst keine Spenden.",
        // Categories
        categories: {
            ClimateAndNature: "Klima & Natur",
            HumanRights: "Menschenrechte",
            Peace: "Frieden",
            Education: "Bildung",
            CleanWater: "Sauberes Wasser",
            FoodSecurity: "Ernährung",
            AnimalWelfare: "Tierschutz",
            MentalHealth: "Gesundheit",
            Refugees: "Geflüchtete",
            Democracy: "Demokratie"
        }
    }
};

// Detect browser language
function detectLanguage() {
    const lang = navigator.language || navigator.userLanguage || 'en';
    const shortLang = lang.split('-')[0].toLowerCase();
    return translations[shortLang] ? shortLang : 'en';
}

const currentLang = detectLanguage();
const t = (key, replacements = {}) => {
    let text = translations[currentLang][key] || translations.en[key] || key;
    for (const [k, v] of Object.entries(replacements)) {
        text = text.replace(`{${k}}`, v);
    }
    return text;
};

// Apply translations to all elements with data-i18n
function applyTranslations() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        el.textContent = t(key);
    });
    // Update page title
    document.title = currentLang === 'de' 
        ? 'OneGood – Tu heute etwas Gutes' 
        : 'OneGood – Do one good thing today';
}

// ============ DOM Elements ============
const elements = {
    loading: document.getElementById('loading'),
    content: document.getElementById('content'),
    actionCard: document.getElementById('action-card'),
    errorState: document.getElementById('error-state'),
    noActionsState: document.getElementById('no-actions-state'),
    completionState: document.getElementById('completion-state'),
    exploreMode: document.getElementById('explore-mode'),

    // Action card elements
    category: document.getElementById('category'),
    headline: document.getElementById('headline'),
    urgencyBlock: document.getElementById('urgency-block'),
    whyNow: document.getElementById('why-now'),
    summary: document.getElementById('summary'),
    storyBadge: document.getElementById('story-badge'),
    storyBadgeOriginal: document.getElementById('story-badge-original'),
    sourceLink: document.getElementById('source-link'),
    impactBlock: document.getElementById('impact-block'),
    impact: document.getElementById('impact'),
    whynowBadge: document.getElementById('whynow-badge'),
    impactBadge: document.getElementById('impact-badge'),
    actionButtons: document.getElementById('action-buttons'),
    actionBtn: document.getElementById('action-btn'),
    shareBtn: document.getElementById('share-btn'),

    // Completion screen
    completionHeadline: document.getElementById('completion-headline'),
    streakDisplay: document.getElementById('streak-display'),
    streakText: document.getElementById('streak-text'),
    exploreMoreBtn: document.getElementById('explore-more-btn'),

    // Explore mode
    backToTodayBtn: document.getElementById('back-to-today-btn'),
    exploreActionCard: document.getElementById('explore-action-card'),

    retryBtn: document.getElementById('retry-btn'),
};

// State
let currentAction = null;
let selectedCategory = '';
let isExploreMode = false;
let selectedActionType = '';

// Category selector
const CATEGORY_KEYS = [
    'ClimateAndNature', 'AnimalWelfare', 'HumanRights', 'Education',
    'Refugees', 'Democracy', 'Peace', 'MentalHealth'
];

let categoryCounts = {}; // { "ClimateAndNature": 5, ... }

async function fetchCategoryCounts() {
    try {
        const response = await fetch(`${API_BASE}/api/actions/category-counts`);
        if (response.ok) {
            categoryCounts = await response.json();
        }
    } catch (e) {
        console.warn('Failed to fetch category counts:', e);
    }
}

// Action type filter
function setupTypePills(containerId, onChange) {
    const container = document.getElementById(containerId);
    if (!container) return;
    container.querySelectorAll('.type-pill').forEach(btn => {
        btn.addEventListener('click', () => {
            container.querySelectorAll('.type-pill').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            onChange(btn.dataset.type || '');
        });
    });
}

function syncTypePillsUI(containerId, activeType) {
    const container = document.getElementById(containerId);
    if (!container) return;
    container.querySelectorAll('.type-pill').forEach(btn => {
        btn.classList.toggle('active', (btn.dataset.type || '') === activeType);
    });
}

function buildCategorySelector() {
    const nav = document.getElementById('category-selector');
    if (!nav) return;

    nav.innerHTML = '';

    const totalCount = Object.values(categoryCounts).reduce((sum, n) => sum + n, 0);

    const allBtn = document.createElement('button');
    allBtn.className = 'category-pill' + (selectedCategory === '' ? ' active' : '');
    allBtn.dataset.category = '';
    allBtn.innerHTML = `${t('allCategories')}<span class="pill-count">${totalCount}</span>`;
    allBtn.addEventListener('click', () => selectCategory(''));
    nav.appendChild(allBtn);

    for (const key of CATEGORY_KEYS) {
        const count = categoryCounts[key] || 0;
        const btn = document.createElement('button');
        const isDisabled = count === 0;
        btn.className = 'category-pill'
            + (selectedCategory === key ? ' active' : '')
            + (isDisabled ? ' disabled' : '');
        btn.dataset.category = key;
        btn.innerHTML = `${formatCategory(key)}<span class="pill-count">${count}</span>`;

        if (isDisabled) {
            btn.disabled = true;
        } else {
            btn.addEventListener('click', () => selectCategory(key));
        }
        nav.appendChild(btn);
    }

    updateFilterLabel();
}

function updateFilterLabel() {
    const label = document.getElementById('filter-label');
    if (!label) return;
    label.textContent = selectedCategory ? formatCategory(selectedCategory) : t('allCategories');
}

let filterToggleInitialized = false;
function setupFilterToggle() {
    if (filterToggleInitialized) return;
    filterToggleInitialized = true;

    const toggle = document.getElementById('filter-toggle');
    const nav = document.getElementById('category-selector');
    if (!toggle || !nav) return;

    toggle.addEventListener('click', () => {
        const isOpen = !nav.classList.contains('hidden');
        nav.classList.toggle('hidden', isOpen);
        toggle.classList.toggle('open', !isOpen);
    });

    document.addEventListener('click', (e) => {
        if (!toggle.contains(e.target) && !nav.contains(e.target)) {
            nav.classList.add('hidden');
            toggle.classList.remove('open');
        }
    });
}

function selectCategory(category) {
    selectedCategory = category;

    document.querySelectorAll('.category-pill').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.category === category);
    });

    const nav = document.getElementById('category-selector');
    const toggle = document.getElementById('filter-toggle');
    if (nav) nav.classList.add('hidden');
    if (toggle) toggle.classList.remove('open');

    updateFilterLabel();

    currentAction = null;
    loadExploreAction();
}

// ============ Streak Tracking ============
function getStreak() {
    try {
        const data = JSON.parse(localStorage.getItem(STORAGE_KEY_STREAK) || '{}');
        return { lastDate: data.lastDate || null, count: data.count || 0 };
    } catch { return { lastDate: null, count: 0 }; }
}

function recordStreak() {
    const today = new Date().toDateString();
    const streak = getStreak();

    if (streak.lastDate === today) return streak; // already recorded today

    const yesterday = new Date(Date.now() - 86400000).toDateString();
    const newCount = (streak.lastDate === yesterday) ? streak.count + 1 : 1;

    const updated = { lastDate: today, count: newCount };
    localStorage.setItem(STORAGE_KEY_STREAK, JSON.stringify(updated));
    return updated;
}

function getTodayCompletion() {
    try {
        const data = JSON.parse(localStorage.getItem(STORAGE_KEY_TODAY) || '{}');
        if (data.date !== new Date().toDateString()) return null;
        return data;
    } catch { return null; }
}

function saveTodayCompletion(headline, category) {
    localStorage.setItem(STORAGE_KEY_TODAY, JSON.stringify({
        date: new Date().toDateString(),
        headline: headline,
        category: category
    }));
}

// Utility functions
function hideAll() {
    elements.loading.classList.add('hidden');
    elements.actionCard.classList.add('hidden');
    elements.errorState.classList.add('hidden');
    elements.noActionsState.classList.add('hidden');
    elements.completionState.classList.add('hidden');
    elements.exploreMode.classList.add('hidden');
    elements.actionButtons.classList.remove('hidden');
    // Hide main type filter when not in main view
    var typeFilter = document.getElementById('type-filter');
    if (typeFilter) typeFilter.classList.add('hidden');
}

function showMainTypeFilter() {
    var typeFilter = document.getElementById('type-filter');
    if (typeFilter) typeFilter.classList.remove('hidden');
}

function setSupportBannerProminent(isProminent) {
    var banner = document.getElementById('support-banner');
    var note = document.getElementById('support-note');
    if (!banner) return;
    banner.classList.toggle('prominent', !!isProminent);
    if (note) note.classList.toggle('hidden', !isProminent);
}

function show(element) {
    elements.content.classList.remove('hidden');
    element.classList.remove('hidden');
    element.classList.add('fade-in');
}

function showToast(message) {
    var toast = document.createElement('div');
    toast.textContent = message;
    toast.className = 'fixed bottom-6 left-1/2 -translate-x-1/2 bg-emerald-600 text-white px-4 py-2 rounded-xl text-sm shadow-lg z-50 transition-opacity duration-300';
    document.body.appendChild(toast);
    setTimeout(function() { toast.style.opacity = '0'; }, 1500);
    setTimeout(function() { toast.remove(); }, 1800);
}

function getStoredCauseIds(storageKey) {
    const data = localStorage.getItem(storageKey);
    if (!data) return [];
    try {
        const parsed = JSON.parse(data);
        const today = new Date().toDateString();
        if (parsed.date !== today) {
            localStorage.removeItem(storageKey);
            return [];
        }
        return parsed.causeIds || [];
    } catch {
        return [];
    }
}

function storeCauseId(storageKey, causeId) {
    const ids = getStoredCauseIds(storageKey);
    if (!ids.includes(causeId)) {
        ids.push(causeId);
    }
    localStorage.setItem(storageKey, JSON.stringify({
        date: new Date().toDateString(),
        causeIds: ids
    }));
}

// "Seen" = cause was shown on screen (visual indicator on revisit)
function getSeenCauseIds() { return getStoredCauseIds(STORAGE_KEY_SEEN); }
function markCauseSeen(causeId) { storeCauseId(STORAGE_KEY_SEEN, causeId); }

// "Acted on" = user clicked Take Action (protected from permanent skip)
function getActedCauseIds() { return getStoredCauseIds(STORAGE_KEY_ACTED); }
function markCauseActed(causeId) { storeCauseId(STORAGE_KEY_ACTED, causeId); }

function getHostFromUrl(url) {
    if (!url) return '';
    try {
        return new URL(url).hostname.replace('www.', '') + ' ↗';
    } catch {
        return '';
    }
}

function stripHtml(html) {
    if (!html) return '';
    const div = document.createElement('div');
    div.innerHTML = html;
    return div.textContent || div.innerText || '';
}

function formatCategory(category) {
    if (!category) return currentLang === 'de' ? 'Projekt' : 'Cause';
    // Use translated category name if available
    const categories = translations[currentLang]?.categories || translations.en.categories;
    return categories[category] || category.replace(/([A-Z])/g, ' $1').trim();
}

// API functions
async function fetchTodaysAction(excludeCurrentCauseId, typeOverride) {
    try {
        let url = `${API_BASE}/api/actions/today?lang=${currentLang}`;
        if (selectedCategory) {
            url += `&category=${selectedCategory}`;
        }
        if (excludeCurrentCauseId) {
            url += `&excludeCurrent=${excludeCurrentCauseId}`;
        }
        var typeParam = typeOverride !== undefined ? typeOverride : selectedActionType;
        if (typeParam) {
            url += `&type=${typeParam}`;
        }
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        const response = await fetch(url, { signal: controller.signal });
        clearTimeout(timeout);
        if (!response.ok) throw new Error('API error');
        return await response.json();
    } catch (error) {
        console.error('Failed to fetch action:', error);
        throw error;
    }
}

async function completeAction(actionId, type) {
    try {
        const response = await fetch(`${API_BASE}/api/actions/${actionId}/complete`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ completionType: type })
        });
        if (!response.ok) throw new Error('Complete failed');
        return await response.json();
    } catch (error) {
        console.error('Failed to complete action:', error);
        return null;
    }
}

async function skipAction(causeId) {
    try {
        await fetch(`${API_BASE}/api/actions/skip`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ causeId })
        });
    } catch (error) {
        console.error('Failed to skip:', error);
    }
}

// Render functions
function renderAction(action) {
    currentAction = action;
    setSupportBannerProminent(false);

    elements.category.textContent = formatCategory(action.causeCategory);
    elements.headline.textContent = action.headline;

    // Why Now
    if (action.whyNow) {
        elements.whyNow.textContent = action.whyNow;
        elements.urgencyBlock.classList.remove('hidden');
        if (elements.whynowBadge) elements.whynowBadge.classList.remove('hidden');
    } else {
        elements.urgencyBlock.classList.add('hidden');
        if (elements.whynowBadge) elements.whynowBadge.classList.add('hidden');
    }

    // Summary
    if (action.causeSummary && action.causeSummary.length > 0) {
        elements.summary.textContent = stripHtml(action.causeSummary);
    } else {
        elements.summary.textContent = stripHtml(action.causeDescription) || '';
    }

    // Show appropriate badge
    if (elements.storyBadge && elements.storyBadgeOriginal) {
        if (action.isAiSummary && action.causeDescription) {
            elements.storyBadge.classList.remove('hidden');
            elements.storyBadgeOriginal.classList.remove('hidden');
            elements.storyBadge.classList.add('active-badge');
            elements.storyBadgeOriginal.classList.remove('active-badge');
        } else {
            elements.storyBadge.classList.add('hidden');
            elements.storyBadgeOriginal.classList.remove('hidden');
            elements.storyBadgeOriginal.classList.add('active-badge');
        }
    }

    // Source link
    if (action.causeUrl) {
        elements.sourceLink.href = action.causeUrl;
        elements.sourceLink.textContent = getHostFromUrl(action.causeUrl);
        elements.sourceLink.classList.remove('hidden');
    } else {
        elements.sourceLink.classList.add('hidden');
    }

    // Impact
    if (action.impactStatement) {
        elements.impact.textContent = action.impactStatement;
        elements.impactBlock.classList.remove('hidden');
        if (elements.impactBadge) elements.impactBadge.classList.remove('hidden');
    } else {
        elements.impactBlock.classList.add('hidden');
        if (elements.impactBadge) elements.impactBadge.classList.add('hidden');
    }

    // Action button
    const buttonIcons = {
        'Donate': '💝',
        'Sign': '✍️',
        'Write': '✉️',
        'Share': '🔗',
        'Learn': '📚'
    };
    const buttonLabels = {
        'Donate': t('donate'),
        'Sign': t('sign'),
        'Write': t('write'),
        'Share': t('share')
    };
    const icon = buttonIcons[action.type] || '🚀';
    const label = buttonLabels[action.type] || t('takeAction');
    elements.actionBtn.innerHTML = icon + ' ' + label;

    hideAll();
    showMainTypeFilter();
    show(elements.actionCard);
}

function renderCompletion(headline, category) {
    const streak = getStreak();
    setSupportBannerProminent(true);
    elements.completionHeadline.textContent = headline || '';

    if (streak.count <= 1) {
        elements.streakText.textContent = t('streakFirstDay');
    } else {
        elements.streakText.textContent = t('streakDay', { count: streak.count }) + ' — ' + t('streakMessage', { count: streak.count });
    }

    hideAll();
    show(elements.completionState);
}

function renderError() {
    setSupportBannerProminent(false);
    hideAll();
    show(elements.errorState);
}

function renderNoActions() {
    setSupportBannerProminent(false);
    hideAll();
    showMainTypeFilter();
    show(elements.noActionsState);
}

// ============ Explore Mode ============
function enterExploreMode() {
    isExploreMode = true;
    setSupportBannerProminent(false);
    selectedCategory = '';
    syncTypePillsUI('explore-type-filter', selectedActionType);
    hideAll();
    show(elements.exploreMode);
    buildCategorySelector();
    setupFilterToggle();
    fetchCategoryCounts().then(() => buildCategorySelector());
    loadExploreAction();
}

function exitExploreMode() {
    isExploreMode = false;
    syncTypePillsUI('type-filter', selectedActionType);
    const completion = getTodayCompletion();
    if (completion) {
        renderCompletion(completion.headline, completion.category);
    } else {
        loadAction();
    }
}

function renderExploreAction(action) {
    currentAction = action;
    const card = elements.exploreActionCard;
    document.getElementById('explore-category').textContent = formatCategory(action.causeCategory);
    document.getElementById('explore-headline').textContent = action.headline;
    document.getElementById('explore-summary').textContent = stripHtml(action.causeSummary || action.causeDescription || '');

    // Why Now
    var urgencyBlock = document.getElementById('explore-urgency-block');
    var whyNowEl = document.getElementById('explore-why-now');
    if (action.whyNow) {
        whyNowEl.textContent = action.whyNow;
        urgencyBlock.classList.remove('hidden');
    } else {
        urgencyBlock.classList.add('hidden');
    }

    // Impact
    var impactBlock = document.getElementById('explore-impact-block');
    var impactEl = document.getElementById('explore-impact');
    if (action.impactStatement) {
        impactEl.textContent = action.impactStatement;
        impactBlock.classList.remove('hidden');
    } else {
        impactBlock.classList.add('hidden');
    }

    // Source link
    var link = document.getElementById('explore-source-link');
    if (action.causeUrl) {
        link.href = action.causeUrl;
        link.textContent = getHostFromUrl(action.causeUrl);
        link.classList.remove('hidden');
    } else {
        link.classList.add('hidden');
    }

    // Action button label
    var exploreBtn = document.getElementById('explore-action-btn');
    var icons = { 'Donate': '💝', 'Sign': '✍️', 'Write': '✉️', 'Share': '🔗' };
    var labels = { 'Donate': t('donate'), 'Sign': t('sign'), 'Write': t('write'), 'Share': t('share') };
    exploreBtn.innerHTML = (icons[action.type] || '🚀') + ' ' + (labels[action.type] || t('takeAction'));

    card.classList.remove('hidden');
    card.classList.add('fade-in');
}

async function loadExploreAction(excludeCurrentCauseId) {
    var card = elements.exploreActionCard;
    card.classList.add('hidden');

    try {
        var url = `${API_BASE}/api/actions/today?lang=${currentLang}`;
        if (selectedCategory) url += `&category=${selectedCategory}`;
        if (selectedActionType) url += `&type=${selectedActionType}`;
        if (excludeCurrentCauseId) url += `&excludeCurrent=${excludeCurrentCauseId}`;
        var response = await fetch(url);
        if (!response.ok) throw new Error('API error');
        var action = await response.json();
        if (!action || !action.hasAction) {
            card.classList.add('hidden');
            return;
        }
        renderExploreAction(action);
    } catch (e) {
        console.error('Explore load failed:', e);
    }
}

// Event handlers
async function handleShare() {
    if (!currentAction) return;
    const url = currentAction.causeUrl || window.location.href;
    const title = currentAction.headline || 'OneGood';
    const text = currentAction.causeSummary || currentAction.headline || '';

    if (navigator.share) {
        try {
            await navigator.share({ title, text, url });
        } catch (e) {
            if (e.name !== 'AbortError') console.error('Share failed', e);
        }
    } else {
        await navigator.clipboard.writeText(url);
        showToast(t('linkCopied'));
    }
}

async function handleAction() {
    if (!currentAction) return;

    // Open the cause URL
    if (currentAction.causeUrl) {
        window.open(currentAction.causeUrl, '_blank');
    }

    // Record completion
    await completeAction(currentAction.actionId, currentAction.type);
    markCauseActed(currentAction.causeId);

    // Update streak
    recordStreak();

    // Save today's completion
    saveTodayCompletion(currentAction.headline, currentAction.causeCategory);

    // Show completion screen — user is DONE for today
    renderCompletion(currentAction.headline, currentAction.causeCategory);
}

async function handleExploreAction() {
    if (!currentAction) return;
    if (currentAction.causeUrl) {
        window.open(currentAction.causeUrl, '_blank');
    }
    await completeAction(currentAction.actionId, currentAction.type);
    markCauseActed(currentAction.causeId);
    // In explore mode, load next cause
    loadExploreAction(currentAction.causeId);
}

async function handleExploreSkip() {
    if (!currentAction) return;
    var causeId = currentAction.causeId;
    skipAction(causeId);
    loadExploreAction(causeId);
}

async function loadAction() {
    hideAll();
    elements.content.classList.add('hidden');
    elements.loading.classList.remove('hidden');

    try {
        // No category filter, no skip — just get the best cause
        const action = await fetchTodaysAction(null);

        if (!action || !action.hasAction) {
            renderNoActions();
            return;
        }

        renderAction(action);
    } catch (error) {
        renderError();
    }
}

// Toggle between AI summary and original description in the story block
function toggleStoryText(showWhich) {
    if (!currentAction) return;

    if (showWhich === 'original' && currentAction.causeDescription) {
        elements.summary.textContent = stripHtml(currentAction.causeDescription);
        elements.storyBadge.classList.remove('active-badge');
        elements.storyBadgeOriginal.classList.add('active-badge');
    } else if (showWhich === 'ai' && currentAction.isAiSummary && currentAction.causeSummary) {
        elements.summary.textContent = stripHtml(currentAction.causeSummary);
        elements.storyBadge.classList.add('active-badge');
        elements.storyBadgeOriginal.classList.remove('active-badge');
    }
}

// ============ SignalR: Real-time AI content updates ============
let hubConnection = null;

function setupSignalR() {
    if (typeof signalR === 'undefined') {
        console.warn('SignalR not available — AI updates will appear on next load');
        return;
    }

    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE}/hubs/causes`)
        .withAutomaticReconnect()
        .build();

    // When background worker finishes generating AI content for a cause,
    // update the current card in-place if it matches
    hubConnection.on('CauseUpdated', (data) => {
        if (!currentAction || !data) return;

        // Only update if this is the cause currently being displayed
        // and the language matches
        if (data.causeId !== currentAction.causeId) return;
        if (data.language !== currentLang) return;

        console.log('Real-time AI update received for current cause:', data.causeId);

        // Update headline (translated by free translation service, not AI)
        if (data.headline && data.headline !== currentAction.headline) {
            currentAction.headline = data.headline;
            elements.headline.textContent = data.headline;
            var exploreHeadline = document.getElementById('explore-headline');
            if (exploreHeadline) exploreHeadline.textContent = data.headline;
        }

        // Update summary if AI generated one
        if (data.summary && data.summary !== currentAction.causeSummary) {
            currentAction.causeSummary = data.summary;
            currentAction.isAiSummary = true;
            elements.summary.textContent = stripHtml(data.summary);
            var exploreSummary = document.getElementById('explore-summary');
            if (exploreSummary) exploreSummary.textContent = stripHtml(data.summary);

            // Show both badges now that AI summary is available
            if (elements.storyBadge && elements.storyBadgeOriginal) {
                elements.storyBadge.classList.remove('hidden');
                elements.storyBadgeOriginal.classList.remove('hidden');
                elements.storyBadge.classList.add('active-badge');
                elements.storyBadgeOriginal.classList.remove('active-badge');
            }
        }

        // Update Why Now
        if (data.whyNow && data.whyNow !== currentAction.whyNow) {
            currentAction.whyNow = data.whyNow;
            elements.whyNow.textContent = data.whyNow;
            elements.urgencyBlock.classList.remove('hidden');
            if (elements.whynowBadge) elements.whynowBadge.classList.remove('hidden');
            var exploreUrgency = document.getElementById('explore-urgency-block');
            var exploreWhyNow = document.getElementById('explore-why-now');
            if (exploreUrgency && exploreWhyNow) {
                exploreWhyNow.textContent = data.whyNow;
                exploreUrgency.classList.remove('hidden');
            }
        }

        // Update Impact
        if (data.impactStatement && data.impactStatement !== currentAction.impactStatement) {
            currentAction.impactStatement = data.impactStatement;
            elements.impact.textContent = data.impactStatement;
            elements.impactBlock.classList.remove('hidden');
            if (elements.impactBadge) elements.impactBadge.classList.remove('hidden');
            var exploreImpactBlock = document.getElementById('explore-impact-block');
            var exploreImpact = document.getElementById('explore-impact');
            if (exploreImpactBlock && exploreImpact) {
                exploreImpact.textContent = data.impactStatement;
                exploreImpactBlock.classList.remove('hidden');
            }
        }
    });

    // When Worker refreshes causes, update category counts live
    hubConnection.on('CategoryCountsUpdated', (counts) => {
        console.log('Category counts updated via SignalR:', counts);
        categoryCounts = counts;
        buildCategorySelector();
    });

    // When Worker imports causes into an empty DB, auto-load the first cause
    hubConnection.on('CausesReady', (data) => {
        console.log('Causes ready:', data.count, 'causes available');
        // If we're currently showing "no actions", reload
        if (!currentAction) {
            loadAction();
        }
    });

    hubConnection.start().catch(err => {
        console.warn('SignalR connection failed:', err);
    });
}

// Initialize
async function init() {
    // Apply translations first
    applyTranslations();

    // Event listeners
    elements.actionBtn.addEventListener('click', handleAction);
    elements.retryBtn.addEventListener('click', () => loadAction());
    if (elements.shareBtn) elements.shareBtn.addEventListener('click', handleShare);

    // Story badge toggle
    if (elements.storyBadge) {
        elements.storyBadge.addEventListener('click', () => toggleStoryText('ai'));
    }
    if (elements.storyBadgeOriginal) {
        elements.storyBadgeOriginal.addEventListener('click', () => toggleStoryText('original'));
    }

    // Action type filter on main view
    setupTypePills('type-filter', function(type) {
        selectedActionType = type;
        currentAction = null;
        loadAction();
    });

    // Action type filter on explore mode
    setupTypePills('explore-type-filter', function(type) {
        selectedActionType = type;
        currentAction = null;
        loadExploreAction();
    });

    // Completion screen → explore mode
    if (elements.exploreMoreBtn) {
        elements.exploreMoreBtn.addEventListener('click', enterExploreMode);
    }

    // Explore mode back button
    if (elements.backToTodayBtn) {
        elements.backToTodayBtn.addEventListener('click', exitExploreMode);
    }

    // Explore mode action/skip buttons
    var exploreActionBtn = document.getElementById('explore-action-btn');
    var exploreSkipBtn = document.getElementById('explore-skip-btn');
    if (exploreActionBtn) exploreActionBtn.addEventListener('click', handleExploreAction);
    if (exploreSkipBtn) exploreSkipBtn.addEventListener('click', handleExploreSkip);

    // Check if user already acted today → show completion screen
    var completion = getTodayCompletion();
    if (completion) {
        renderCompletion(completion.headline, completion.category);
    } else {
        // Load today's ONE cause
        loadAction();
    }

    // Connect to SignalR for real-time AI content updates
    setupSignalR();
}

// ============ Legal Modal Logic ============
function showLegalModal(type) {
    const modal = document.getElementById('legal-modal');
    const content = document.getElementById('legal-modal-content');
    let html = '';
    if (type === 'imprint') {
        html = `<h2 class='text-lg font-bold mb-2'>${t('legalImprintTitle')}</h2>
            <p class='mb-2'>${t('legalImprintIntro')}</p>
            <p class='mb-2'>${t('legalImprintName')}</p>
            <p>${t('legalImprintEmail')}</p>`;
    } else if (type === 'privacy') {
        html = `<h2 class='text-lg font-bold mb-2'>${t('legalPrivacyTitle')}</h2>
            <p class='mb-2'>${t('legalPrivacyIntro')}</p>
            <p class='mb-2'>${t('legalPrivacyDataPoints')}</p>
            <p class='mb-2'>${t('legalPrivacyDataList')}</p>
            <p class='mb-2'>${t('legalPrivacyUsage')}</p>
            <p class='mb-2'>${t('legalPrivacyAnon')}</p>
            <p>${t('legalPrivacyContact')}</p>`;
    } else if (type === 'external') {
        html = `<h2 class='text-lg font-bold mb-2'>${t('legalExternalTitle')}</h2>
            <p class='mb-2'>${t('legalExternalDisclaimer')}</p>
            <p>${t('legalExternalNoDonations')}</p>`;
    }
    content.innerHTML = html;
    modal.classList.remove('hidden');
}

document.addEventListener('DOMContentLoaded', function() {
    applyTranslations();
    if (document.getElementById('footer-year')) {
        document.getElementById('footer-year').textContent = new Date().getFullYear();
    }
    const imprint = document.getElementById('footer-imprint-link');
    const privacy = document.getElementById('footer-privacy-link');
    const external = document.getElementById('footer-external-link');
    if (imprint) imprint.addEventListener('click', function(e) { e.preventDefault(); showLegalModal('imprint'); });
    if (privacy) privacy.addEventListener('click', function(e) { e.preventDefault(); showLegalModal('privacy'); });
    if (external) external.addEventListener('click', function(e) { e.preventDefault(); showLegalModal('external'); });
    const close = document.getElementById('legal-modal-close');
    if (close) close.addEventListener('click', function() {
        document.getElementById('legal-modal').classList.add('hidden');
    });
});

document.addEventListener('DOMContentLoaded', init);
})();
