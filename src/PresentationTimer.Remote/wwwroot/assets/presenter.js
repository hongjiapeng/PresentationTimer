(() => {
  "use strict";

  const translations = {
    en: {
      title: "Presentation Timer Remote", app: "Presentation Timer", language: "Language",
      connecting: "Connecting…", connected: "Connected", reconnecting: "Reconnecting…",
      disconnected: "Disconnected", expired: "Session expired",
      slide: (current, total) => `Slide ${current} / ${total}`,
      noSlideShow: "No slide show is running",
      showEnded: "Slide show ended",
      remaining: "Remaining", overtime: "Overtime", notes: "Speaker notes",
      emptyNotes: "No speaker notes on this slide.", navigation: "Slide navigation",
      noActiveSlideNotes: "Speaker notes will appear when the slide show starts.",
      previous: "Previous", next: "Next",
    },
    "zh-CN": {
      title: "演示计时器遥控", app: "演示计时器", language: "语言",
      connecting: "正在连接…", connected: "已连接", reconnecting: "正在重新连接…",
      disconnected: "连接已断开", expired: "会话已过期",
      slide: (current, total) => `第 ${current} / ${total} 页`,
      noSlideShow: "当前未放映",
      showEnded: "放映已结束",
      remaining: "剩余时间", overtime: "超时", notes: "演讲者备注",
      emptyNotes: "此页没有演讲者备注。", navigation: "幻灯片导航",
      noActiveSlideNotes: "开始放映后，这里会显示演讲者备注。",
      previous: "上一页", next: "下一页",
    },
    "zh-TW": {
      title: "簡報計時器遙控", app: "簡報計時器", language: "語言",
      connecting: "正在連線…", connected: "已連線", reconnecting: "正在重新連線…",
      disconnected: "連線已中斷", expired: "工作階段已過期",
      slide: (current, total) => `第 ${current} / ${total} 張`,
      noSlideShow: "目前未放映",
      showEnded: "放映已結束",
      remaining: "剩餘時間", overtime: "逾時", notes: "演講者備忘稿",
      emptyNotes: "這張投影片沒有備忘稿。", navigation: "投影片導覽",
      noActiveSlideNotes: "開始放映後，這裡會顯示演講者備忘稿。",
      previous: "上一張", next: "下一張",
    },
    ja: {
      title: "プレゼンタイマー リモコン", app: "プレゼンタイマー", language: "言語",
      connecting: "接続中…", connected: "接続済み", reconnecting: "再接続中…",
      disconnected: "切断されました", expired: "セッションの有効期限が切れました",
      slide: (current, total) => `スライド ${current} / ${total}`,
      noSlideShow: "スライドショーは実行されていません",
      showEnded: "スライドショーが終了しました",
      remaining: "残り時間", overtime: "超過時間", notes: "発表者ノート",
      emptyNotes: "このスライドに発表者ノートはありません。", navigation: "スライド操作",
      noActiveSlideNotes: "スライドショーを開始すると、発表者ノートが表示されます。",
      previous: "前へ", next: "次へ",
    },
    es: {
      title: "Control del temporizador", app: "Temporizador de presentación", language: "Idioma",
      connecting: "Conectando…", connected: "Conectado", reconnecting: "Reconectando…",
      disconnected: "Desconectado", expired: "Sesión caducada",
      slide: (current, total) => `Diapositiva ${current} / ${total}`,
      noSlideShow: "No hay una presentación en curso",
      showEnded: "La presentación ha terminado",
      remaining: "Tiempo restante", overtime: "Tiempo excedido", notes: "Notas del orador",
      emptyNotes: "Esta diapositiva no tiene notas.", navigation: "Navegación de diapositivas",
      noActiveSlideNotes: "Las notas aparecerán al iniciar la presentación.",
      previous: "Anterior", next: "Siguiente",
    },
    fr: {
      title: "Télécommande du minuteur", app: "Minuteur de présentation", language: "Langue",
      connecting: "Connexion…", connected: "Connecté", reconnecting: "Reconnexion…",
      disconnected: "Déconnecté", expired: "Session expirée",
      slide: (current, total) => `Diapositive ${current} / ${total}`,
      noSlideShow: "Aucun diaporama en cours",
      showEnded: "Le diaporama est terminé",
      remaining: "Temps restant", overtime: "Temps dépassé", notes: "Notes du présentateur",
      emptyNotes: "Aucune note sur cette diapositive.", navigation: "Navigation des diapositives",
      noActiveSlideNotes: "Les notes apparaîtront au démarrage du diaporama.",
      previous: "Précédente", next: "Suivante",
    },
    de: {
      title: "Präsentationstimer Fernbedienung", app: "Präsentationstimer", language: "Sprache",
      connecting: "Verbinden…", connected: "Verbunden", reconnecting: "Erneut verbinden…",
      disconnected: "Getrennt", expired: "Sitzung abgelaufen",
      slide: (current, total) => `Folie ${current} / ${total}`,
      noSlideShow: "Keine Bildschirmpräsentation aktiv",
      showEnded: "Bildschirmpräsentation beendet",
      remaining: "Verbleibende Zeit", overtime: "Überzeit", notes: "Sprechernotizen",
      emptyNotes: "Keine Notizen auf dieser Folie.", navigation: "Foliennavigation",
      noActiveSlideNotes: "Notizen erscheinen nach dem Start der Bildschirmpräsentation.",
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
  let navigationPendingFromSlide = null;
  let navigationWaitTimer = null;
  let connectionState = "connecting";
  let presentationEnded = false;
  let strings;

  const hasCurrentSlide = () =>
    latestState?.presentationStatus === "Running" &&
    Number.isInteger(latestState.currentSlideIndex) &&
    Number.isInteger(latestState.totalSlides) &&
    latestState.currentSlideIndex >= 1 &&
    latestState.currentSlideIndex <= latestState.totalSlides;

  const updateNavigation = () => {
    const canNavigate = connectionState === "connected" && hasCurrentSlide() &&
      !invocationPending && navigationPendingFromSlide === null;
    previous.disabled = !canNavigate || latestState.currentSlideIndex <= 1;
    next.disabled = !canNavigate || latestState.currentSlideIndex >= latestState.totalSlides;
  };

  const releaseNavigationWait = () => {
    if (navigationWaitTimer !== null) clearTimeout(navigationWaitTimer);
    navigationWaitTimer = null;
    navigationPendingFromSlide = null;
    updateNavigation();
  };

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
    if (state !== "connected" && navigationPendingFromSlide !== null) releaseNavigationWait();
    updateNavigation();
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
      notes.textContent = strings.noActiveSlideNotes;
      notes.classList.add("is-empty");
      timerMode.textContent = strings.remaining;
      updateNavigation();
      return;
    }
    const slideIsActive = hasCurrentSlide();
    slidePosition.textContent = slideIsActive
      ? strings.slide(latestState.currentSlideIndex, latestState.totalSlides)
      : presentationEnded && latestState.presentationStatus === "NoSlideShow"
        ? strings.showEnded
        : strings.noSlideShow;
    notes.textContent = slideIsActive
      ? latestState.speakerNotes || strings.emptyNotes
      : strings.noActiveSlideNotes;
    notes.classList.toggle("is-empty", !slideIsActive || !latestState.speakerNotes);
    timerMode.textContent = latestState.isOvertime ? strings.overtime : strings.remaining;
    timerValue.textContent = formatTime(latestState.timerDisplaySeconds);
    timer.classList.toggle("overtime", latestState.isOvertime);
    updateNavigation();
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
    if (latestState?.presentationStatus === "Running" && state.presentationStatus === "NoSlideShow") {
      presentationEnded = true;
    } else if (state.presentationStatus === "Running") {
      presentationEnded = false;
    }
    latestRevision = state.revision;
    latestState = state;
    if (navigationPendingFromSlide !== null &&
        (state.presentationStatus !== "Running" || state.currentSlideIndex !== navigationPendingFromSlide)) {
      releaseNavigationWait();
    }
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
    try {
      applyState(await connection.invoke("GetState"));
      setConnectionState("connected");
    } catch {
      setConnectionState("disconnected");
    }
  });
  connection.onclose((error) => setConnectionState(error ? "expired" : "disconnected"));

  const navigate = async (method) => {
    const button = method === "Previous" ? previous : next;
    if (button.disabled || invocationPending || connection.state !== signalR.HubConnectionState.Connected) return;
    invocationPending = true;
    navigationPendingFromSlide = latestState.currentSlideIndex;
    navigationWaitTimer = setTimeout(releaseNavigationWait, 1000);
    updateNavigation();
    try {
      const result = await connection.invoke(method);
      if (!result?.isSuccess) {
        releaseNavigationWait();
        applyState(await connection.invoke("GetState"));
      }
    } catch {
      releaseNavigationWait();
      if (connection.state !== signalR.HubConnectionState.Connected) {
        setConnectionState("disconnected");
      }
    } finally {
      invocationPending = false;
      if (connection.state === signalR.HubConnectionState.Connected && connectionState !== "connected") {
        setConnectionState("connected");
      }
      updateNavigation();
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
