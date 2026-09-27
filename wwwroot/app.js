document.getElementById("loginForm").addEventListener("submit", async (e) => {
  e.preventDefault();

  const usernameInput = document.getElementById("username").value;
  const passwordInput = document.getElementById("password").value;
  const messageDiv = document.getElementById("message");

  messageDiv.style.display = "none";
  messageDiv.className = "";

  try {
    const response = await fetch("/api/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        userName: usernameInput,
        password: passwordInput,
      }),
    });

    const data = await response.json();

    if (response.ok) {
      messageDiv.textContent = `${data.message} (ようこそ ${data.userName} さん)`;
      messageDiv.className = "success";
      messageDiv.style.display = "block";
    } else {
      messageDiv.textContent = data.message || "ログインに失敗しました。";
      messageDiv.className = "error";
      messageDiv.style.display = "block";
    }
  } catch (error) {
    messageDiv.textContent = "通信エラーが発生しました。";
    messageDiv.className = "error";
    messageDiv.style.display = "block";
  }
});
