(function () {
  async function loadUsers(form) {
    var url = form.getAttribute('data-users-url');
    var inventoryId = form.getAttribute('data-inventory-id');
    var select = form.querySelector('select[name="targetUserId"]');
    if (!url || !select) {
      return;
    }

    if (inventoryId) {
      var separator = url.indexOf('?') === -1 ? '?' : '&';
      url = url + separator + 'inventoryId=' + encodeURIComponent(inventoryId);
    }

    try {
      var response = await fetch(url, { headers: { 'Accept': 'application/json' } });
      if (!response.ok) {
        return;
      }

      var users = await response.json();
      var placeholder = select.querySelector('option[value=""]');
      var placeholderText = placeholder ? placeholder.textContent : 'Select a user';
      select.innerHTML = '';
      var emptyOption = document.createElement('option');
      emptyOption.value = '';
      emptyOption.textContent = placeholderText;
      select.appendChild(emptyOption);
      users.forEach(function (user) {
        var option = document.createElement('option');
        option.value = user.id;
        option.textContent = user.name + (user.email ? ' (' + user.email + ')' : '');
        select.appendChild(option);
      });
    } catch (error) {
      // Fail quietly so the page remains usable even if lookup fails.
    }
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form[data-user-picker]').forEach(function (form) {
      loadUsers(form);
    });
  });
})();
