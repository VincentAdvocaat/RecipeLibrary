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
  }
};
