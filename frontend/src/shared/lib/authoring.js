/*
 * Helpers shared by the course builder (tutor pages, learner module) and the organisation course editor (sponsor module).
 */

export const OFFERING_FORMATS = [
  ['self_paced', 'Self-paced'],
  ['cohort', 'Cohort (dated, live classes)'],
  ['one_to_one', 'One-to-one'],
];
export const FUNDING_TYPES = [
  ['self', 'Learner pays'],
  ['sponsored', 'Accepts sponsorship'],
  ['employer', 'Employer-funded'],
];

/** Splits a comma/newline separated string into trimmed, non-empty, unique items. */
export function splitList(value) {
  return [...new Set(String(value || '').split(/[,\n]/).map((s) => s.trim()).filter(Boolean))];
}

/** Local datetime-local input value → ISO string (UTC). */
export function toIso(local) {
  if (!local) return null;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

/** PUT a file to a presigned URL with progress (0–100). Resolves when the storage provider answers 2xx. */
export function uploadToPresignedUrl(url, file, contentType, onProgress) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', url);
    xhr.setRequestHeader('Content-Type', contentType || file.type || 'application/octet-stream');
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress?.(Math.round((e.loaded / e.total) * 100));
    xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`Upload failed (${xhr.status})`)));
    xhr.onerror = () => reject(new Error('Upload failed: the storage service could not be reached.'));
    xhr.send(file);
  });
}
