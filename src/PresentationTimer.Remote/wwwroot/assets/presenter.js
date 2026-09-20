(() => {
  "use strict";

  const translations = {
    en: {
      title: "Presentation Timer Remote", app: "Presentation Timer", language: "Language",
      connecting: "Connecting…", connected: "Connected", reconnecting: "Reconnecting…",
      disconnected: "Disconnected", expired: "Session expired",
      slide: (current, total) => `Slide ${current} / ${total}`,
      remaining: "Remaining", overtime: "Overtime", notes: "Speaker notes",
      emptyNotes: "No speaker notes on this slide.", navigation: "Slide navigation",
      previous: "Previous", next: "Next",
    },
    "zh-CN": {
      title: "演示计时器遥控", app: "演示计时器", language: "语言",
      connecting: "正在连接…", connected: "已连接", reconnecting: "正在重新连接…",
      disconnected: "连接已断开", expired: "会话已过期",
      slide: (current, total) => `第 ${current} / ${total} 页`,
      remaining: "剩余时间", overtime: "超时", notes: "演讲者备注",
      emptyNotes: "此页没有演讲者备注。", navigation: "幻灯片导航",
      previous: "上一页", next: "下一页",
    },
    "zh-TW": {
      title: "簡報計時器遙控", app: "簡報計時器", language: "語言",
      connecting: "正在連線…", connected: "已連線", reconnecting: "正在重新連線…",
      disconnected: "連線已中斷", expired: "工作階段已過期",
      slide: (current, total) => `第 ${current} / ${total} 張`,
      remaining: "剩餘時間", overtime: "逾時", notes: "演講者備忘稿",
      emptyNotes: "這張投影片沒有備忘稿。", navigation: "投影片導覽",
      previous: "上一張", next: "下一張",
    },
    ja: {
      title: "プレゼンタイマー リモコン", app: "プレゼンタイマー", language: "言語",
      connecting: "接続中…", connected: "接続済み", reconnecting: "再接続中…",
      disconnected: "切断されました", expired: "セッションの有効期限が切れました",
      slide: (current, total) => `スライド ${current} / ${total}`,
      remaining: "残り時間", overtime: "超過時間", notes: "発表者ノート",
      emptyNotes: "このスライドに発表者ノートはありません。", navigation: "スライド操作",
      previous: "前へ", next: "次へ",
    },
    es: {
      title: "Control del temporizador", app: "Temporizador de presentación", language: "Idioma",
      connecting: "Conectando…", connected: "Conectado", reconnecting: "Reconectando…",
      disconnected: "Desconectado", expired: "Sesión caducada",
      slide: (current, total) => `Diapositiva ${current} / ${total}`,
      remaining: "Tiempo restante", overtime: "Tiempo excedido", notes: "Notas del orador",
      emptyNotes: "Esta diapositiva no tiene notas.", navigation: "Navegación de diapositivas",
      previous: "Anterior", next: "Siguiente",
    },
    fr: {
      title: "Télécommande du minuteur", app: "Minuteur de présentation", language: "Langue",
      connecting: "Connexion…", connected: "Connecté", reconnecting: "Reconnexion…",
      disconnected: "Déconnecté", expired: "Session expirée",
      slide: (current, total) => `Diapositive ${current} / ${total}`,
      remaining: "Temps restant", overtime: "Temps dépassé", notes: "Notes du présentateur",
      emptyNotes: "Aucune note sur cette diapositive.", navigation: "Navigation des diapositives",
      previous: "Précédente", next: "Suivante",
    },
    de: {
      title: "Präsentationstimer Fernbedienung", app: "Präsentationstimer", language: "Sprache",
      connecting: "Verbinden…", connected: "Verbunden", reconnecting: "Erneut verbinden…",
      disconnected: "Getrennt", expired: "Sitzung abgelaufen",
      slide: (current, total) => `Folie ${current} / ${total}`,
      remaining: "Verbleibende Zeit", overtime: "Überzeit", notes: "Sprechernotizen",
      emptyNotes: "Keine Notizen auf dieser Folie.", navigation: "Foliennavigation",
      previous: "Zurück", next: "Weiter",
    },
  };

  const connectionLabel = document.getElementById("connection");
  const slidePosition = document.getElementById("slide-position");
  const timer = document.querySelector(".timer");
  const timerMode = document.getElementById("timer-mode");
  const timerValue = document.getElementById("timer-value");
  const notes = document.getElementById("notes");
  const previous = document.getElementById("previous");
  const next = document.getElementById("next");
  const languageControl = document.getElementById("language-control");
  const languageToggle = document.getElementById("language-toggle");
  const languagePopover = document.getElementById("language-popover");
  const languageOptions = document.querySelectorAll(".language-option");
  let latestRevision = -1;
  let latestState = null;
  let invocationPending = false;
  let connectionState = "connecting";
  let strings;

  const browserLocale = () => {
    const preferred = navigator.languages?.length ? navigator.languages : [navigator.language || "en"];
    for (const tag of preferred) {
      const normalized = tag.toLowerCase();
      if (normalized.startsWith("zh")) {
        return /(?:hant|tw|hk|mo)/.test(normalized) ? "zh-TW" : "zh-CN";
      }
      const base = normalized.split("-")[0];
      if (translations[base]) return base;
    }
    return "en";
  };

  const setConnectionState = (state) => {
    connectionState = state;
    connectionLabel.textContent = strings[state];
    connectionLabel.dataset.state = state;
    // The pending guard blocks duplicate commands without flashing the disabled button style.
    const canNavigate = state === "connected";
    previous.disabled = !canNavigate;
    next.disabled = !canNavigate;
  };

  const formatTime = (totalSeconds) => {
    const value = Math.max(0, Number(totalSeconds) || 0);
    const hours = Math.floor(value / 3600);
    const minutes = Math.floor((value % 3600) / 60);
    const seconds = value % 60;
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`
      : `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  };

  const renderState = () => {
    if (!latestState) {
      slidePosition.textContent = strings.slide("—", "—");
      notes.textContent = strings.emptyNotes;
      notes.classList.add("is-empty");
      timerMode.textContent = strings.remaining;
      return;
    }
    slidePosition.textContent = strings.slide(
      latestState.currentSlideIndex ?? "—",
      latestState.totalSlides ?? "—",
    );
    notes.textContent = latestState.speakerNotes || strings.emptyNotes;
    notes.classList.toggle("is-empty", !latestState.speakerNotes);
    timerMode.textContent = latestState.isOvertime ? strings.overtime : strings.remaining;
    timerValue.textContent = formatTime(latestState.timerDisplaySeconds);
    timer.classList.toggle("overtime", latestState.isOvertime);
  };

  const setLocale = (locale) => {
    strings = translations[locale];
    for (const option of languageOptions) {
      if (option.dataset.locale === locale) {
        option.setAttribute("aria-current", "true");
      } else {
        option.removeAttribute("aria-current");
      }
    }
    document.documentElement.lang = locale;
    document.title = strings.title;
    document.getElementById("app-name").textContent = strings.app;
    document.getElementById("language-label").textContent = strings.language;
    document.getElementById("language-heading").textContent = strings.language;
    languageToggle.title = strings.language;
    document.getElementById("notes-heading").textContent = strings.notes;
    document.querySelector(".navigation").setAttribute("aria-label", strings.navigation);
    previous.querySelector(".button-label").textContent = strings.previous;
    next.querySelector(".button-label").textContent = strings.next;
    setConnectionState(connectionState);
    renderState();
  };

  const applyState = (state) => {
    if (!state || state.revision < latestRevision) return;
    latestRevision = state.revision;
    latestState = state;
    renderState();
  };

  const setLanguageMenuOpen = (open) => {
    languagePopover.hidden = !open;
    languageToggle.setAttribute("aria-expanded", String(open));
  };

  languageToggle.addEventListener("click", () => {
    setLanguageMenuOpen(languagePopover.hidden);
  });
  for (const option of languageOptions) {
    option.addEventListener("click", () => {
      setLocale(option.dataset.locale);
      setLanguageMenuOpen(false);
      languageToggle.focus();
    });
  }
  document.addEventListener("pointerdown", (event) => {
    if (!languageControl.contains(event.target)) setLanguageMenuOpen(false);
  });
  languageControl.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setLanguageMenuOpen(false);
      languageToggle.focus();
    }
  });
  setLocale(browserLocale());

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/presenterHub")
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds(context) {
        const delays = [0, 1000, 2000, 5000, 10000, 30000];
        return delays[Math.min(context.previousRetryCount, delays.length - 1)];
      },
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on("stateChanged", applyState);
  connection.onreconnecting(() => setConnectionState("reconnecting"));
  connection.onreconnected(async () => {
    setConnectionState("connected");
    applyState(await connection.invoke("GetState"));
  });
  connection.onclose((error) => setConnectionState(error ? "expired" : "disconnected"));

  const navigate = async (method) => {
    if (invocationPending || connection.state !== signalR.HubConnectionState.Connected) return;
    invocationPending = true;
    try {
      await connection.invoke(method);
    } catch {
      if (connection.state !== signalR.HubConnectionState.Connected) {
        setConnectionState("disconnected");
      }
    } finally {
      invocationPending = false;
      if (connection.state === signalR.HubConnectionState.Connected && connectionState !== "connected") {
        setConnectionState("connected");
      }
    }
  };

  previous.addEventListener("click", () => navigate("Previous"));
  next.addEventListener("click", () => navigate("Next"));

  const start = async () => {
    setConnectionState("connecting");
    try {
      await connection.start();
      setConnectionState("connected");
      applyState(await connection.invoke("GetState"));
    } catch {
      setConnectionState("expired");
    }
  };

  start();
})();
