// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Small UI enhancements: theme toggle and preserved choice in cookie
(function () {
	function setTheme(theme) {
		if (theme === 'dark') {
			document.documentElement.setAttribute('data-theme', 'dark');
		} else {
			document.documentElement.setAttribute('data-theme', 'light');
		}
		// Persist cookie for 1 year
		var expires = new Date();
		expires.setFullYear(expires.getFullYear() + 1);
		document.cookie = 'theme=' + theme + ';path=/;expires=' + expires.toUTCString() + ';SameSite=Lax';
	}

	document.addEventListener('DOMContentLoaded', function () {
		var btn = document.getElementById('theme-toggle');
		if (!btn) return;
		btn.addEventListener('click', function () {
			var current = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
			var next = current === 'dark' ? 'light' : 'dark';
			setTheme(next);
			// Update button label from localized data attributes
			try {
				var label = next === 'dark' ? btn.getAttribute('data-label-dark') : btn.getAttribute('data-label-light');
				if (label) btn.textContent = label;
			} catch { }
		});
	});
})();
