"use strict";

const HOST_NAME = "com.screentranslator.browser_bridge";
const RECONNECT_MIN_MS = 1000;
const RECONNECT_MAX_MS = 30000;
const browserKind = navigator.userAgent.includes("Edg/") ? "edge" : "chrome";

let nativePort;
let reconnectTimer;
let reconnectDelayMs = RECONNECT_MIN_MS;
let lastGeneration = Date.now();
const documentsByFrame = new Map();
const activeTabByWindow = new Map();
let lastFocusedWindowId = null;

function frameKey(tabId, frameId) {
  return `${tabId}:${frameId}`;
}

function nextGeneration() {
  const now = Date.now();
  lastGeneration = Math.max(now, lastGeneration + 1);
  return lastGeneration;
}

function rememberDocument(
  tabId,
  frameId,
  token,
  devicePixelRatio,
  viewportSize) {
  const key = frameKey(tabId, frameId);
  const current = documentsByFrame.get(key);
  if (current && current.documentToken === token) {
    current.devicePixelRatio = devicePixelRatio;
    if (viewportSize) {
      current.viewportSize = viewportSize;
    }
    return current;
  }

  const state = {
    documentToken: token,
    navigationGeneration: nextGeneration(),
    devicePixelRatio,
    viewportSize: viewportSize ?? null
  };
  documentsByFrame.set(key, state);
  return state;
}

function forgetTab(tabId) {
  const prefix = `${tabId}:`;
  for (const key of documentsByFrame.keys()) {
    if (key.startsWith(prefix)) {
      documentsByFrame.delete(key);
    }
  }
}

function invalidateTopFrame(tabId, windowId, reason) {
  const state = documentsByFrame.get(frameKey(tabId, 0));
  if (state) {
    postNative({
      type: "invalidated",
      browserWindowId: windowId,
      tabId,
      documentToken: state.documentToken,
      navigationGeneration: state.navigationGeneration,
      reason,
      frameId: 0
    });
  }
  forgetTab(tabId);
}

function rememberActiveTab(tabId, windowId) {
  activeTabByWindow.set(windowId, tabId);
  lastFocusedWindowId = windowId;
}

function markConnectionHealthy() {
  reconnectDelayMs = RECONNECT_MIN_MS;
}

function scheduleReconnect() {
  if (reconnectTimer !== undefined || nativePort !== undefined) {
    return;
  }

  const delay = reconnectDelayMs;
  reconnectDelayMs = Math.min(
    reconnectDelayMs * 2,
    RECONNECT_MAX_MS);
  reconnectTimer = setTimeout(() => {
    reconnectTimer = undefined;
    connectNativeHost();
  }, delay);
}

function connectNativeHost() {
  if (nativePort !== undefined) {
    return;
  }

  let connectedPort;
  try {
    connectedPort = chrome.runtime.connectNative(HOST_NAME);
  } catch {
    scheduleReconnect();
    return;
  }

  nativePort = connectedPort;
  connectedPort.onMessage.addListener(message => {
    markConnectionHealthy();
    if (message && message.type === "queryActiveTab") {
      void answerActiveTabQuery(message);
    }
  });
  connectedPort.onDisconnect.addListener(() => {
    void chrome.runtime.lastError;
    if (nativePort === connectedPort) {
      nativePort = undefined;
    }
    scheduleReconnect();
  });

  postNative({
    type: "bridgeReady",
    browser: browserKind
  });
  void announceFocusedDocument();
}

function postNative(message) {
  if (nativePort === undefined) {
    return false;
  }

  try {
    nativePort.postMessage(message);
    return true;
  } catch {
    nativePort = undefined;
    scheduleReconnect();
    return false;
  }
}

function validDocumentMessage(message) {
  return (
    message &&
    typeof message.documentToken === "string" &&
    message.documentToken.length >= 16 &&
    message.documentToken.length <= 256 &&
    Number.isFinite(message.devicePixelRatio) &&
    message.devicePixelRatio >= 0.5 &&
    message.devicePixelRatio <= 8
  );
}

function normalizeBounds(browserWindow) {
  const values = [
    browserWindow.left,
    browserWindow.top,
    browserWindow.width,
    browserWindow.height
  ];
  if (!values.every(Number.isFinite)) {
    return null;
  }
  if (
    Math.abs(browserWindow.left) > 1000000 ||
    Math.abs(browserWindow.top) > 1000000 ||
    browserWindow.width <= 0 ||
    browserWindow.height <= 0 ||
    browserWindow.width > 1000000 ||
    browserWindow.height > 1000000
  ) {
    return null;
  }

  return {
    left: browserWindow.left,
    top: browserWindow.top,
    width: browserWindow.width,
    height: browserWindow.height
  };
}

async function forwardScrollBatch(message, sender) {
  if (
    !sender.tab ||
    sender.tab.active !== true ||
    !Number.isInteger(sender.tab.id) ||
    !Number.isInteger(sender.tab.windowId) ||
    !validDocumentMessage(message) ||
    !Array.isArray(message.scrolls)
  ) {
    return;
  }

  const frameId = Number.isInteger(sender.frameId) ? sender.frameId : 0;
  const state = rememberDocument(
    sender.tab.id,
    frameId,
    message.documentToken,
    message.devicePixelRatio,
    null);

  for (const scroll of message.scrolls) {
    if (
      !scroll ||
      typeof scroll.target !== "string" ||
      scroll.target.length === 0 ||
      scroll.target.length > 256 ||
      !Number.isFinite(scroll.deltaXCss) ||
      !Number.isFinite(scroll.deltaYCss) ||
      Math.abs(scroll.deltaXCss) > 100000 ||
      Math.abs(scroll.deltaYCss) > 100000
    ) {
      continue;
    }

    const scrollContainer = scroll.target === "root"
      ? null
      : normalizeScrollContainer(scroll.scrollContainer);
    if (scroll.target !== "root" && !scrollContainer) {
      continue;
    }

    postNative({
      type: "scroll",
      browserWindowId: sender.tab.windowId,
      tabId: sender.tab.id,
      documentToken: state.documentToken,
      navigationGeneration: state.navigationGeneration,
      deltaXCss: scroll.deltaXCss,
      deltaYCss: scroll.deltaYCss,
      devicePixelRatio: message.devicePixelRatio,
      scrollContainer,
      targetId: scroll.target,
      frameId
    });
  }
}

function normalizeScrollContainer(container) {
  if (container == null) {
    return null;
  }

  const values = [
    container.left,
    container.top,
    container.width,
    container.height
  ];
  if (
    !values.every(Number.isFinite) ||
    Math.abs(container.left) > 1000000 ||
    Math.abs(container.top) > 1000000 ||
    container.width <= 0 ||
    container.height <= 0 ||
    container.width > 1000000 ||
    container.height > 1000000
  ) {
    return null;
  }

  return {
    left: container.left,
    top: container.top,
    width: container.width,
    height: container.height
  };
}

async function forwardInvalidation(message, sender) {
  if (
    !sender.tab ||
    sender.tab.active !== true ||
    !Number.isInteger(sender.tab.id) ||
    !Number.isInteger(sender.tab.windowId) ||
    !message ||
    typeof message.documentToken !== "string" ||
    message.documentToken.length === 0 ||
    message.documentToken.length > 256
  ) {
    return;
  }

  const frameId = Number.isInteger(sender.frameId) ? sender.frameId : 0;
  const key = frameKey(sender.tab.id, frameId);
  const state = documentsByFrame.get(key);
  if (!state || state.documentToken !== message.documentToken) {
    return;
  }

  documentsByFrame.delete(key);
  if (frameId !== 0) {
    return;
  }

  postNative({
    type: "invalidated",
    browserWindowId: sender.tab.windowId,
    tabId: sender.tab.id,
    documentToken: state.documentToken,
    navigationGeneration: state.navigationGeneration,
    reason: typeof message.reason === "string"
      ? message.reason
      : "documentInvalidated",
    frameId
  });
}

async function forwardHello(message, sender) {
  if (
    !sender.tab ||
    sender.tab.active !== true ||
    !Number.isInteger(sender.tab.id) ||
    !Number.isInteger(sender.tab.windowId) ||
    !validDocumentMessage(message) ||
    !normalizeSize(message.viewportSize)
  ) {
    return;
  }

  const frameId = Number.isInteger(sender.frameId) ? sender.frameId : 0;
  const viewportSize = normalizeSize(message.viewportSize);
  const state = rememberDocument(
    sender.tab.id,
    frameId,
    message.documentToken,
    message.devicePixelRatio,
    viewportSize);
  if (frameId !== 0) {
    return;
  }

  let browserWindow;
  try {
    browserWindow = await chrome.windows.get(sender.tab.windowId);
  } catch {
    return;
  }
  const browserWindowBounds = normalizeBounds(browserWindow);
  if (!browserWindowBounds) {
    return;
  }

  postHello(
    sender.tab,
    frameId,
    state,
    browserWindowBounds);
}

function postHello(tab, frameId, state, browserWindowBounds) {
  if (!state.viewportSize) {
    return false;
  }

  const posted = postNative({
    type: "hello",
    browser: browserKind,
    browserWindowId: tab.windowId,
    tabId: tab.id,
    documentToken: state.documentToken,
    navigationGeneration: state.navigationGeneration,
    devicePixelRatio: state.devicePixelRatio,
    viewportSize: state.viewportSize,
    browserWindowBounds,
    frameId
  });
  if (posted) {
    markConnectionHealthy();
  }
  return posted;
}

function normalizeSize(size) {
  if (
    !size ||
    !Number.isFinite(size.width) ||
    !Number.isFinite(size.height) ||
    size.width <= 0 ||
    size.height <= 0 ||
    size.width > 1000000 ||
    size.height > 1000000
  ) {
    return null;
  }

  return {
    width: size.width,
    height: size.height
  };
}

async function ensureTopFrameState(tab) {
  const key = frameKey(tab.id, 0);
  const current = documentsByFrame.get(key);
  if (current) {
    return current;
  }

  let response;
  try {
    response = await chrome.tabs.sendMessage(
      tab.id,
      { type: "queryDocumentState" },
      { frameId: 0 });
  } catch {
    return null;
  }

  const viewportSize = normalizeSize(response?.viewportSize);
  if (!validDocumentMessage(response) || !viewportSize) {
    return null;
  }
  return rememberDocument(
    tab.id,
    0,
    response.documentToken,
    response.devicePixelRatio,
    viewportSize);
}

async function announceFocusedDocument() {
  try {
    const browserWindow = await chrome.windows.getLastFocused();
    const tabs = await chrome.tabs.query({
      active: true,
      windowId: browserWindow.id
    });
    const tab = tabs[0];
    if (
      !tab ||
      !Number.isInteger(tab.id) ||
      !Number.isInteger(tab.windowId)
    ) {
      return;
    }

    await announceTab(tab, browserWindow);
  } catch {
    // Unsupported/internal pages simply remain in static translation mode.
  }
}

async function announceTab(tab, browserWindow = null) {
  if (
    !tab ||
    !Number.isInteger(tab.id) ||
    !Number.isInteger(tab.windowId)
  ) {
    return false;
  }

  let targetWindow = browserWindow;
  if (!targetWindow) {
    try {
      targetWindow = await chrome.windows.get(tab.windowId);
    } catch {
      return false;
    }
  }

  const browserWindowBounds = normalizeBounds(targetWindow);
  if (!browserWindowBounds) {
    return false;
  }

  const state = await ensureTopFrameState(tab);
  if (!state) {
    return false;
  }

  rememberActiveTab(tab.id, tab.windowId);
  return postHello(tab, 0, state, browserWindowBounds);
}

async function answerActiveTabQuery(query) {
  const requestId = typeof query.requestId === "string"
    ? query.requestId
    : null;

  try {
    const browserWindow = await chrome.windows.getLastFocused();
    const browserWindowBounds = normalizeBounds(browserWindow);
    const tabs = await chrome.tabs.query({
      active: true,
      windowId: browserWindow.id
    });
    const tab = tabs[0];
    if (
      !browserWindowBounds ||
      !tab ||
      !Number.isInteger(tab.id) ||
      !Number.isInteger(tab.windowId)
    ) {
      postActiveTabUnavailable(requestId);
      return;
    }

    const state = await ensureTopFrameState(tab);
    if (!state) {
      postActiveTabUnavailable(requestId);
      return;
    }

    rememberActiveTab(tab.id, tab.windowId);
    postNative({
      type: "activeTab",
      requestId,
      found: true,
      browser: browserKind,
      browserWindowId: tab.windowId,
      browserWindowBounds,
      tabId: tab.id,
      frameId: 0,
      documentToken: state.documentToken,
      navigationGeneration: state.navigationGeneration,
      devicePixelRatio: state.devicePixelRatio,
      viewportSize: state.viewportSize
    });
  } catch {
    postActiveTabUnavailable(requestId);
  }
}

function postActiveTabUnavailable(requestId) {
  postNative({
    type: "activeTab",
    requestId,
    found: false,
    browser: browserKind
  });
}

chrome.runtime.onMessage.addListener((message, sender) => {
  if (!message || typeof message.type !== "string") {
    return false;
  }

  if (message.type === "contentReady") {
    void forwardHello(message, sender);
  } else if (message.type === "scrollBatch") {
    void forwardScrollBatch(message, sender);
  } else if (message.type === "contentInvalidated") {
    void forwardInvalidation(message, sender);
  }
  return false;
});

chrome.tabs.onActivated.addListener(async activeInfo => {
  const previousTabId = activeTabByWindow.get(activeInfo.windowId);
  if (
    Number.isInteger(previousTabId) &&
    previousTabId !== activeInfo.tabId
  ) {
    invalidateTopFrame(
      previousTabId,
      activeInfo.windowId,
      "activeTabChanged");
  }

  let tab;
  try {
    tab = await chrome.tabs.get(activeInfo.tabId);
  } catch {
    return;
  }
  await announceTab(tab);
});

chrome.tabs.onRemoved.addListener((tabId, removeInfo) => {
  if (activeTabByWindow.get(removeInfo.windowId) === tabId) {
    activeTabByWindow.delete(removeInfo.windowId);
    invalidateTopFrame(tabId, removeInfo.windowId, "activeTabClosed");
  } else {
    forgetTab(tabId);
  }
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status !== "loading") {
    return;
  }

  if (Number.isInteger(tab.windowId)) {
    invalidateTopFrame(tabId, tab.windowId, "navigationStarted");
  } else {
    forgetTab(tabId);
  }
});

chrome.windows.onFocusChanged.addListener(async windowId => {
  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    return;
  }

  if (
    Number.isInteger(lastFocusedWindowId) &&
    lastFocusedWindowId !== windowId
  ) {
    const previousTabId = activeTabByWindow.get(lastFocusedWindowId);
    if (Number.isInteger(previousTabId)) {
      invalidateTopFrame(
        previousTabId,
        lastFocusedWindowId,
        "browserWindowFocusChanged");
    }
  }

  try {
    const tabs = await chrome.tabs.query({
      active: true,
      windowId
    });
    await announceTab(tabs[0]);
  } catch {
    // A closing or internal browser window has no trackable document.
  }
});

connectNativeHost();
