let currentUser = null;

// 今日の日付および当月を初期値としてセット
const workDateEl = document.getElementById("work-date");
if (workDateEl) {
  workDateEl.value = new Date().toISOString().substring(0, 10);
}

const summaryYearMonthEl = document.getElementById("summary-year-month");
if (summaryYearMonthEl) {
  summaryYearMonthEl.value = new Date().toISOString().substring(0, 7);
}

// ログイン処理
const loginForm = document.getElementById("login-form");
if (loginForm) {
  loginForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const username = document.getElementById("username").value;
    const password = document.getElementById("password").value;
    const msgDiv = document.getElementById("login-message");

    try {
      const response = await fetch("/api/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });
      const data = await response.json();

      if (response.ok) {
        currentUser = data;
        document.getElementById("user-display-name").textContent =
          data.displayName || data.username || username;
        document.getElementById("login-section").classList.add("hidden");
        document.getElementById("dashboard-section").classList.remove("hidden");
        await loadConstructions();
      } else {
        msgDiv.innerHTML = `<div class="alert alert-danger">${data.message || "ログイン失敗"}</div>`;
      }
    } catch (err) {
      console.error("ログインエラー:", err);
      msgDiv.innerHTML = `<div class="alert alert-danger">通信エラーが発生しました。</div>`;
    }
  });
}

// ログアウト
const logoutBtn = document.getElementById("logout-btn");
if (logoutBtn) {
  logoutBtn.addEventListener("click", () => {
    currentUser = null;
    document.getElementById("dashboard-section").classList.add("hidden");
    document.getElementById("login-section").classList.remove("hidden");
  });
}

// タブ切り替え（HTMLからの onclick 呼び出しに対応）
function switchTab(tabId) {
  document
    .querySelectorAll(".tab-content")
    .forEach((el) => el.classList.add("hidden"));
  document
    .querySelectorAll(".nav-tab")
    .forEach((el) => el.classList.remove("active"));

  const targetTab = document.getElementById(tabId);
  if (targetTab) {
    targetTab.classList.remove("hidden");
  }

  if (window.event && window.event.target) {
    window.event.target.classList.add("active");
  }

  // 集計タブが開かれたら自動で集計取得
  if (tabId === "tab-summary") {
    fetchAndRenderSummary();
  }
}

// 工事番号取得 API の呼び出し
async function loadConstructions() {
  const select = document.getElementById("construction-select");
  if (!select) return;
  try {
    const response = await fetch("/api/constructions");
    const list = await response.json();
    select.innerHTML =
      '<option value="">-- 工事番号を選択してください --</option>';
    list.forEach((item) => {
      const opt = document.createElement("option");
      opt.value = item.constructionId;
      opt.textContent = `[${item.constructionCode}] ${item.constructionName}`;
      select.appendChild(opt);
    });

    // 1件目の工事番号を初期選択
    if (list.length > 0) {
      select.value = list[0].constructionId;
    }
  } catch (err) {
    console.error("工事番号取得エラー:", err);
    select.innerHTML = '<option value="">読み込み失敗</option>';
  }
}

// 勤務データ登録処理
const workRecordForm = document.getElementById("work-record-form");
if (workRecordForm) {
  workRecordForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const msgDiv = document.getElementById("work-message");
    const constructionSelect = document.getElementById("construction-select");
    const selectedConstructionId = constructionSelect
      ? parseInt(constructionSelect.value, 10)
      : NaN;

    if (isNaN(selectedConstructionId) || selectedConstructionId <= 0) {
      msgDiv.innerHTML = `<div class="alert alert-danger">工事番号を選択してください。</div>`;
      return;
    }

    const payload = {
      userId: currentUser ? currentUser.userId || 1 : 1,
      workDate: document.getElementById("work-date").value,
      startTime: document.getElementById("start-time").value,
      endTime: document.getElementById("end-time").value,
      overtimeHours: parseFloat(document.getElementById("overtime").value) || 0,
      constructionId: selectedConstructionId,
      remarks: document.getElementById("remarks").value || "",
    };

    console.log("送信データ確認:", payload);

    try {
      const response = await fetch("/api/work-records", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const data = await response.json();

      if (response.ok) {
        msgDiv.innerHTML = `<div class="alert alert-success">${data.message || "勤務データを登録しました。"}</div>`;
        document.getElementById("remarks").value = "";
      } else {
        msgDiv.innerHTML = `<div class="alert alert-danger">${data.message || "登録失敗"}</div>`;
      }
    } catch (err) {
      console.error("登録エラー:", err);
      msgDiv.innerHTML = `<div class="alert alert-danger">登録通信エラーが発生しました。</div>`;
    }
  });
}

// 集計サマリー表示API呼び出し処理
async function fetchAndRenderSummary() {
  const yearMonthInput = document.getElementById("summary-year-month");
  const msgDiv = document.getElementById("summary-message");
  if (!yearMonthInput) return;

  const yearMonth = yearMonthInput.value;
  if (!yearMonth) {
    msgDiv.innerHTML = `<div class="alert alert-danger">年月を選択してください。</div>`;
    return;
  }

  const userId = currentUser ? currentUser.userId || 1 : 1;

  try {
    const response = await fetch(`/api/work-records/summary?userId=${userId}&yearMonth=${yearMonth}`);
    if (!response.ok) {
      throw new Error("集計データの取得に失敗しました。");
    }

    const summary = await response.json();

    // 出勤日数・残業時間の表示更新
    document.getElementById("total-work-days").textContent = summary.totalWorkDays || 0;
    document.getElementById("total-overtime-hours").textContent = summary.totalOvertimeHours || 0;

    // 工事別内訳テーブル更新
    const tbody = document.getElementById("construction-breakdown-body");
    tbody.innerHTML = "";

    if (summary.constructionBreakdown && summary.constructionBreakdown.length > 0) {
      summary.constructionBreakdown.forEach((item) => {
        const tr = document.createElement("tr");
        tr.style.borderBottom = "1px solid #dee2e6";
        tr.innerHTML = `
          <td style="padding: 8px;">${item.constructionCode || "-"}</td>
          <td style="padding: 8px;">${item.constructionName || "-"}</td>
          <td style="padding: 8px; text-align: right;">${item.totalHours || 0} 時間</td>
        `;
        tbody.appendChild(tr);
      });
    } else {
      tbody.innerHTML = '<tr><td colspan="3" style="padding: 8px; text-align: center;">指定した年月のデータが存在しません。</td></tr>';
    }
    msgDiv.innerHTML = "";
  } catch (err) {
    console.error("集計取得エラー:", err);
    msgDiv.innerHTML = `<div class="alert alert-danger">集計データの読み込みに失敗しました。</div>`;
  }
}

// 集計ボタン押下イベント listener
const loadSummaryBtn = document.getElementById("load-summary-btn");
if (loadSummaryBtn) {
  loadSummaryBtn.addEventListener("click", fetchAndRenderSummary);
}
