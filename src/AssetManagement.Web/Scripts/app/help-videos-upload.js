/*
 * Hidden how-to video page (/{tenant}/HelpVideos): chunked upload for Settings.Manage users.
 * The file is sent in 4 MB pieces (IIS allows about 28.6 MB per request), each with the antiforgery token,
 * then UploadComplete checks the size and file header and saves the row. A failed piece is retried twice.
 * IE11-safe: var / function only, XMLHttpRequest + FormData + Blob.slice.
 */
(function () {
    'use strict';

    var form = document.getElementById('help-video-upload-form');
    if (!form) {
        return;
    }

    var topic = document.getElementById('help-video-topic');
    var title = document.getElementById('help-video-title');
    var fileInput = document.getElementById('help-video-file');
    var uploadButton = document.getElementById('help-video-upload-button');
    var cancelButton = document.getElementById('help-video-cancel-button');
    var statusEl = document.getElementById('help-video-upload-status');
    var progress = document.getElementById('help-video-upload-progress');
    var bar = progress ? progress.querySelector('.progress-bar') : null;
    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    var chunkSize = parseInt(form.getAttribute('data-chunk-size'), 10) || (4 * 1024 * 1024);
    var maxBytes = parseFloat(form.getAttribute('data-max-bytes')) || (500 * 1024 * 1024);
    var current = null;

    function setStatus(text, isError) {
        statusEl.textContent = text || '';
        statusEl.className = 'am-help-upload-status' + (isError ? ' text-danger' : '');
    }

    function setProgress(done, total) {
        if (!bar) {
            return;
        }
        var pct = total > 0 ? Math.floor((done / total) * 100) : 0;
        bar.style.width = pct + '%';
        bar.setAttribute('aria-valuenow', String(pct));
        bar.textContent = pct + '%';
    }

    function setBusy(busy) {
        uploadButton.disabled = busy;
        topic.disabled = busy;
        title.disabled = busy;
        fileInput.disabled = busy;
        cancelButton.hidden = !busy;
        if (progress) {
            progress.hidden = !busy && !current;
        }
    }

    function post(url, data, onDone) {
        var xhr = new XMLHttpRequest();
        xhr.open('POST', url, true);
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4 || xhr.amAborted) {
                return;
            }
            var body = null;
            try {
                body = JSON.parse(xhr.responseText);
            } catch (e) {
                body = null;
            }
            if (xhr.status >= 200 && xhr.status < 300 && body && body.ok) {
                onDone(null, body);
                return;
            }
            var message = body && body.error ? body.error
                : (xhr.status === 403 ? 'You do not have permission to upload videos.'
                    : (xhr.status === 0 ? 'The connection was lost.' : 'The server returned an error (' + xhr.status + ').'));
            onDone({ message: message, retry: xhr.status === 0 || xhr.status >= 500 }, body);
        };
        if (current) {
            current.xhr = xhr;
        }
        xhr.send(data);
    }

    function formData() {
        var data = new FormData();
        if (tokenInput) {
            data.append('__RequestVerificationToken', tokenInput.value);
        }
        return data;
    }

    function fail(message) {
        var state = current;
        current = null;
        setBusy(false);
        if (progress) {
            progress.hidden = true;
        }
        setStatus(message, true);
        if (state && state.uploadId) {
            var data = formData();
            data.append('uploadId', state.uploadId);
            post(form.getAttribute('data-cancel-url'), data, function () { });
        }
    }

    function sendChunk(offset, attempt) {
        var state = current;
        if (!state || state.cancelled) {
            return;
        }
        var file = state.file;
        if (offset >= file.size) {
            complete();
            return;
        }
        var end = Math.min(offset + chunkSize, file.size);
        var data = formData();
        data.append('uploadId', state.uploadId);
        data.append('offset', String(offset));
        data.append('chunk', file.slice(offset, end), 'chunk');
        post(form.getAttribute('data-chunk-url'), data, function (err, body) {
            if (!current || current.cancelled) {
                return;
            }
            if (err) {
                if (err.retry && attempt < 2) {
                    setStatus('Retrying piece at ' + Math.round(offset / 1048576) + ' MB...', false);
                    window.setTimeout(function () { sendChunk(offset, attempt + 1); }, 1500 * (attempt + 1));
                    return;
                }
                fail(err.message);
                return;
            }
            var received = typeof body.received === 'number' ? body.received : end;
            setProgress(received, file.size);
            setStatus('Uploading... ' + Math.round(received / 1048576) + ' of ' + Math.round(file.size / 1048576) + ' MB', false);
            sendChunk(received, 0);
        });
    }

    function complete() {
        var data = formData();
        data.append('uploadId', current.uploadId);
        setStatus('Checking the video...', false);
        post(form.getAttribute('data-complete-url'), data, function (err, body) {
            if (err) {
                fail(err.message);
                return;
            }
            current = null;
            setStatus(body.replaced ? 'Video replaced. Reloading...' : 'Video uploaded. Reloading...', false);
            window.location.reload();
        });
    }

    uploadButton.addEventListener('click', function () {
        var file = fileInput.files && fileInput.files.length ? fileInput.files[0] : null;
        if (!topic.value) {
            setStatus('Choose a topic for the video.', true);
            return;
        }
        if (!file) {
            setStatus('Choose an MP4 or WebM file.', true);
            return;
        }
        if (!/\.(mp4|webm)$/i.test(file.name)) {
            setStatus('Only .mp4 and .webm videos can be uploaded.', true);
            return;
        }
        if (file.size <= 0 || file.size > maxBytes) {
            setStatus('The file must be between 1 byte and ' + Math.round(maxBytes / 1048576) + ' MB.', true);
            return;
        }
        current = { file: file, uploadId: null, cancelled: false, xhr: null };
        setBusy(true);
        setProgress(0, file.size);
        setStatus('Starting upload...', false);
        var data = formData();
        data.append('topicSlug', topic.value);
        data.append('title', title.value || '');
        data.append('fileName', file.name);
        data.append('totalBytes', String(file.size));
        post(form.getAttribute('data-start-url'), data, function (err, body) {
            if (err) {
                fail(err.message);
                return;
            }
            current.uploadId = body.uploadId;
            if (body.chunkSize) {
                chunkSize = body.chunkSize;
            }
            sendChunk(0, 0);
        });
    });

    cancelButton.addEventListener('click', function () {
        if (!current) {
            return;
        }
        current.cancelled = true;
        if (current.xhr) {
            try {
                current.xhr.amAborted = true;
                current.xhr.abort();
            } catch (e) {
                // ignore
            }
        }
        fail('Upload cancelled.');
    });
}());
