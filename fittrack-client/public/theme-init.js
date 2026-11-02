// İlk boyamadan önce kayıtlı temayı uygular — yoksa koyu tema seçen kullanıcı sayfa her açıldığında
// bir an beyaz ekran görür. CSP satır içi betiğe izin vermediği için ayrı dosya (script-src 'self').
(function () {
  var theme = "light";
  try { if (localStorage.getItem("fittrack:theme") === "dark") theme = "dark"; } catch (e) {}
  document.documentElement.dataset.theme = theme;
  if (theme === "dark") document.documentElement.classList.add("dark");
})();
