(() => {
  "use strict";

  const connectionLabel = document.getElementById("connection");
  const slidePosition = document.getElementById("slide-position");
  const timer = document.querySelector(".timer");
  const timerMode = document.getElementById("timer-mode");
  const timerValue = document.getElementById("timer-value");
  const notes = document.getElementById("notes");
  const previous = document.getElementById("previous");
  const next = document.getElementById("next");
  let latestRevision = -1;
  let invocationPending = false;

  const setConnectionState = (state) => {
    connectionLabel.textContent = state[0].toUpperCase() + state.slice(1);
    connectionLabel.dataset.state = state;
    const canNavigate = state === "connected" && !invocationPending;
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

  const applyState = (state) => {
    if (!state || state.revision < latestRevision) return;
    latestRevision = state.revision;
    slidePosition.textContent = state.currentSlideIndex == null
      ? "Slide — / —"
      : `Slide ${state.currentSlideIndex} / ${state.totalSlides}`;
    notes.textContent = state.speakerNotes || "No speaker notes on this slide.";
    timerMode.textContent = state.isOvertime ? "Overtime" : "Remaining";
    timerValue.textContent = formatTime(state.timerDisplaySeconds);
    timer.classList.toggle("overtime", state.isOvertime);
  };

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
    setConnectionState("connected");
    try {
      await connection.invoke(method);
    } catch {
      setConnectionState("disconnected");
    } finally {
      invocationPending = false;
      if (connection.state === signalR.HubConnectionState.Connected) setConnectionState("connected");
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
