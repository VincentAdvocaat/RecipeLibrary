window.RecipeLibrary = window.RecipeLibrary || {};

window.RecipeLibrary.photoUploadZone = {
  init: function (elementRef, dotNetRef) {
    const el = elementRef;
    if (!el) return;

    function prevent(e) {
      e.preventDefault();
      e.stopPropagation();
    }

    el.addEventListener('dragover', function (e) {
      prevent(e);
      el.classList.add('border-gray-500', 'bg-gray-50');
    });
    el.addEventListener('dragleave', function (e) {
      prevent(e);
      el.classList.remove('border-gray-500', 'bg-gray-50');
    });
    el.addEventListener('drop', function (e) {
      prevent(e);
      el.classList.remove('border-gray-500', 'bg-gray-50');
      const file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
      if (!file || !file.type.startsWith('image/')) return;

      const formData = new FormData();
      formData.append('file', file);

      fetch('/api/upload-recipe-image', {
        method: 'POST',
        body: formData
      })
        .then(function (r) { return r.ok ? r.json() : Promise.reject(new Error('Upload failed')); })
        .then(function (data) {
          if (data && data.url) dotNetRef.invokeMethodAsync('SetImageUrlFromDrop', data.url);
        })
        .catch(function () {
          dotNetRef.invokeMethodAsync('SetUploadError', 'Upload mislukt.');
        });
    });

    // Click to open file explorer: hidden input + zone click triggers it
    const accept = 'image/jpeg,image/png,image/gif,image/webp';
    const input = document.createElement('input');
    input.setAttribute('type', 'file');
    input.setAttribute('accept', accept);
    input.style.position = 'absolute';
    input.style.left = '-9999px';
    input.style.visibility = 'hidden';
    el.appendChild(input);

    el.addEventListener('click', function (e) {
      if (e.target.closest('button')) return;
      input.click();
    });

    input.addEventListener('change', function () {
      const file = input.files && input.files[0];
      input.value = '';
      if (!file || !file.type.startsWith('image/')) return;

      const formData = new FormData();
      formData.append('file', file);

      fetch('/api/upload-recipe-image', {
        method: 'POST',
        body: formData
      })
        .then(function (r) { return r.ok ? r.json() : Promise.reject(new Error('Upload failed')); })
        .then(function (data) {
          if (data && data.url) dotNetRef.invokeMethodAsync('SetImageUrlFromDrop', data.url);
        })
        .catch(function () {
          dotNetRef.invokeMethodAsync('SetUploadError', 'Upload mislukt.');
        });
    });
  }
};
