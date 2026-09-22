const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = fs.readFileSync(path.join(__dirname, "../wwwroot/assets/presenter.js"), "utf8");

function createElement() {
  const listeners = new Map();
  const attributes = new Map();
  return {
    textContent: "",
    dataset: {},
    disabled: false,
    hidden: false,
    classList: { add() {}, toggle() {} },
    addEventListener(name, listener) { listeners.set(name, listener); },
    setAttribute(name, value) { attributes.set(name, value); },
    removeAttribute(name) { attributes.delete(name); },
    querySelector() { return createElement(); },
    click() { return listeners.get("click")?.(); },
    contains() { return false; },
    focus() {},
  };
}

async function createPresenter(initialState) {
  const ids = [
    "connection", "slide-position", "timer-mode", "timer-value", "notes",
    "previous", "next", "language-control", "language-toggle",
    "language-popover", "app-name", "language-label", "language-heading", "notes-heading",
  ];
  const elements = Object.fromEntries(ids.map((id) => [id, createElement()]));
  const timer = createElement();
  const navigation = createElement();
  const handlers = new Map();
  const commands = [];
  const connection = {
    state: "Connected",
    currentState: initialState,
    commandResult: { isSuccess: true },
    on(name, listener) { handlers.set(name, listener); },
    onreconnecting(listener) { this.reconnecting = listener; },
    onreconnected(listener) { this.reconnected = listener; },
    onclose(listener) { this.closed = listener; },
    async start() {},
    async invoke(method) {
      if (method === "GetState") return this.currentState;
      commands.push(method);
      return this.commandResult;
    },
  };
  const document = {
    documentElement: {},
    getElementById(id) { return elements[id]; },
    querySelector(selector) { return selector === ".timer" ? timer : navigation; },
    querySelectorAll() { return []; },
    addEventListener() {},
  };
  const signalR = {
    HubConnectionState: { Connected: "Connected" },
    LogLevel: { Warning: 1 },
    HubConnectionBuilder: class {
      withUrl() { return this; }
      withAutomaticReconnect() { return this; }
      configureLogging() { return this; }
      build() { return connection; }
    },
  };

  vm.runInNewContext(source, {
    document, navigator: { languages: ["zh-CN"] }, signalR, setTimeout, clearTimeout,
  });
  await new Promise((resolve) => setImmediate(resolve));
  return { elements, connection, commands, update: handlers.get("stateChanged") };
}

const running = (revision, currentSlideIndex, totalSlides = 3) => ({
  revision,
  presentationStatus: "Running",
  currentSlideIndex,
  totalSlides,
  speakerNotes: "Test notes",
  isOvertime: false,
  timerDisplaySeconds: 120,
});

test("navigation follows the active slide and disables both controls after the show ends", async () => {
  const presenter = await createPresenter(running(1, 1));
  const { previous, next, notes } = presenter.elements;
  const position = presenter.elements["slide-position"];

  assert.equal(previous.disabled, true);
  assert.equal(next.disabled, false);

  presenter.update(running(2, 2));
  assert.equal(previous.disabled, false);
  assert.equal(next.disabled, false);

  await previous.click();
  assert.deepEqual(presenter.commands, ["Previous"]);

  presenter.update(running(3, 3));
  assert.equal(previous.disabled, false);
  assert.equal(next.disabled, true);

  presenter.update({ ...running(4, null), presentationStatus: "NoSlideShow", totalSlides: null, speakerNotes: "" });
  assert.equal(previous.disabled, true);
  assert.equal(next.disabled, true);
  assert.equal(position.textContent, "放映已结束");
  assert.equal(notes.textContent, "开始放映后，这里会显示演讲者备注。");
  await next.click();
  assert.deepEqual(presenter.commands, ["Previous"]);

  presenter.update(running(5, 1, 1));
  assert.equal(previous.disabled, true);
  assert.equal(next.disabled, true);
});

test("reconnecting disables navigation until an authoritative state is loaded", async () => {
  const presenter = await createPresenter(running(1, 2));
  presenter.connection.reconnecting();
  assert.equal(presenter.elements.previous.disabled, true);
  assert.equal(presenter.elements.next.disabled, true);

  await presenter.connection.reconnected();
  assert.equal(presenter.elements.previous.disabled, false);
  assert.equal(presenter.elements.next.disabled, false);
});

test("a rejected navigation command resynchronizes the finished slide show", async () => {
  const presenter = await createPresenter(running(1, 2));
  presenter.connection.currentState = {
    ...running(2, null),
    presentationStatus: "NoSlideShow",
    totalSlides: null,
    speakerNotes: "",
  };
  presenter.connection.commandResult = { isSuccess: false };

  await presenter.elements.next.click();
  assert.deepEqual(presenter.commands, ["Next"]);
  assert.equal(presenter.elements.previous.disabled, true);
  assert.equal(presenter.elements.next.disabled, true);
  assert.equal(presenter.elements["slide-position"].textContent, "放映已结束");
});

test("rapid repeated taps cannot queue another command before the slide state changes", async () => {
  // Slide 5 is hidden in the fixture, so Next from 4 reaches the final slide 6.
  const presenter = await createPresenter(running(1, 4, 6));

  await presenter.elements.next.click();
  await presenter.elements.next.click();
  assert.deepEqual(presenter.commands, ["Next"]);
  assert.equal(presenter.elements.next.disabled, true);

  presenter.update(running(2, 6, 6));
  assert.equal(presenter.elements.next.disabled, true);
  assert.equal(presenter.elements.previous.disabled, false);
});
