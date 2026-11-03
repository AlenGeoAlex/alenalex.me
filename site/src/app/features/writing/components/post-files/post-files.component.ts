import { ChangeDetectionStrategy, Component, PLATFORM_ID, inject, input, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { PostFile } from '@core/models/post.model';

type Os = 'mac' | 'linux' | 'windows';

/** A command that checks the file against the expected hash itself: prints OK / True, or fails. */
const CHECKS: Record<Os, { label: string; command: (name: string, sha256: string) => string }> = {
  mac: { label: 'macOS', command: (name, sha) => `echo "${sha}  ${name}" | shasum -a 256 -c` },
  linux: { label: 'Linux', command: (name, sha) => `echo "${sha}  ${name}" | sha256sum -c` },
  windows: { label: 'PowerShell', command: (name, sha) => `(Get-FileHash "${name}" -Algorithm SHA256).Hash -eq "${sha.toUpperCase()}"` },
};

function detectOs(): Os {
  const platform = (navigator as Navigator & { userAgentData?: { platform?: string } }).userAgentData?.platform ?? navigator.platform ?? '';
  const ua = `${platform} ${navigator.userAgent}`.toLowerCase();
  if (ua.includes('win')) return 'windows';
  if (ua.includes('mac') || ua.includes('iphone') || ua.includes('ipad')) return 'mac';
  return 'linux';
}

/** Clipboard API first; where it's blocked (embedded browsers), the old copy command on a hidden textarea. */
async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.cssText = 'position:fixed;opacity:0;pointer-events:none';
    document.body.append(area);
    area.select();
    const ok = document.execCommand('copy');
    area.remove();
    return ok;
  }
}

type DownloadState =
  | { phase: 'idle' }
  | { phase: 'downloading'; percent: number | null }
  | { phase: 'verifying' }
  | { phase: 'verified' }
  | { phase: 'mismatch'; got: string }
  | { phase: 'failed' };

/**
 * A post's downloads. Files from assets/ are fetched by the browser, hashed (SHA-256, Web Crypto) and
 * only saved if the hash matches the one worked out at build time from the repo copy. Bigger files
 * live in GitHub Releases and are plain links, with the hash to check by hand.
 */
@Component({
  selector: 'app-post-files',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './post-files.component.html',
})
export class PostFilesComponent {
  readonly files = input.required<PostFile[]>();

  private readonly states = signal<Record<string, DownloadState>>({});
  protected readonly copied = signal<string | null>(null);
  protected readonly copiedCheck = signal<string | null>(null);
  /** the reader's OS, for the check command (macOS while prerendering) */
  protected readonly os = CHECKS[isPlatformBrowser(inject(PLATFORM_ID)) ? detectOs() : 'mac'];

  protected state(file: PostFile): DownloadState {
    return this.states()[file.url] ?? { phase: 'idle' };
  }

  /** A new hash means a new URL, so a replaced file never comes back stale from the CDN cache. */
  protected href(file: PostFile): string {
    return `${file.url}?v=${file.sha256.slice(0, 12)}`;
  }

  protected size(bytes: number | null): string {
    if (bytes === null) return '';
    const units = ['B', 'KB', 'MB', 'GB'];
    let n = bytes;
    let u = 0;
    while (n >= 1024 && u < units.length - 1) {
      n /= 1024;
      u++;
    }
    return `${n.toFixed(u === 0 || n >= 100 ? 0 : 1)} ${units[u]}`;
  }

  protected async copyHash(file: PostFile): Promise<void> {
    if (!(await copyText(file.sha256))) return;
    this.copied.set(file.url);
    setTimeout(() => this.copied.set(null), 1600);
  }

  protected async copyCheck(file: PostFile): Promise<void> {
    if (!(await copyText(this.os.command(file.name, file.sha256)))) return;
    this.copiedCheck.set(file.url);
    setTimeout(() => this.copiedCheck.set(null), 2000);
  }

  protected async download(file: PostFile): Promise<void> {
    const set = (s: DownloadState) => this.states.update((all) => ({ ...all, [file.url]: s }));
    set({ phase: 'downloading', percent: null });
    try {
      const response = await fetch(this.href(file), { mode: 'cors' });
      if (!response.ok || !response.body) throw new Error(`HTTP ${response.status}`);

      const total = Number(response.headers.get('content-length')) || file.size || 0;
      const reader = response.body.getReader();
      const chunks: Uint8Array[] = [];
      let received = 0;
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        received += value.length;
        set({ phase: 'downloading', percent: total ? Math.min(100, Math.round((received / total) * 100)) : null });
      }

      set({ phase: 'verifying' });
      const blob = new Blob(chunks as BlobPart[]);
      const digest = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer());
      const got = Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, '0')).join('');
      if (got !== file.sha256) {
        // not saved: the bytes aren't the file in the repo
        set({ phase: 'mismatch', got });
        return;
      }

      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = file.name;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
      set({ phase: 'verified' });
    } catch {
      // network or CORS: offer the direct links instead
      set({ phase: 'failed' });
    }
  }
}
