document.addEventListener("DOMContentLoaded", async () => {
  const statusEl = document.getElementById("status");

  try {
    const response = await fetch("/api/health");

    if (!response.ok) {
      throw new Error(`HTTPエラー ステータス: ${response.status}`);
    }

    // テキストを取得し、トリム（前後の空白除去）
    const text = (await response.text()).trim();

    if (!text) {
      statusEl.textContent =
        "APIから空のレスポンスが返されました（Program.vbの戻り値を確認してください）";
      statusEl.style.color = "#ed6c02"; // 警告時のオレンジ色
      return;
    }

    try {
      // JSONパースを試行
      const data = JSON.parse(text);
      const message = data.message || data.Message || text;
      statusEl.textContent = message;
    } catch {
      // テキストの場合はそのまま表示
      statusEl.textContent = text;
    }

    statusEl.style.color = "#2e7d32"; // 成功時の緑色
  } catch (error) {
    console.error("通信詳細エラー:", error);
    statusEl.textContent = `API通信エラー: ${error.message}`;
    statusEl.style.color = "#c62828"; // エラー時の赤色
  }
});
