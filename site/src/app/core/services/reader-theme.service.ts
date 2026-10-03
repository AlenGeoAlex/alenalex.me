import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type ReaderTheme = 'dark' | 'light';

const STORAGE_KEY = 'reader-theme';

/** Light or dark paper for reading posts. Remembered per visitor. */
@Injectable({ providedIn: 'root' })
export class ReaderThemeService {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = signal<ReaderTheme>(this.read());

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: ReaderTheme): void {
    this.theme.set(theme);
    if (!this.browser) return;
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // storage can be blocked; the toggle still works for this visit
    }
  }

  private read(): ReaderTheme {
    if (!this.browser) return 'dark';
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'light' || saved === 'dark') return saved;
    } catch {
      // ignore
    }
    return matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }
}
