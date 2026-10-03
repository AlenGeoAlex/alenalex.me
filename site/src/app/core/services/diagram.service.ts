import { Injectable } from '@angular/core';
import type { MermaidConfig } from 'mermaid';
import { ReaderTheme } from './reader-theme.service';

const FONT = "'JetBrains Mono', ui-monospace, monospace";

const THEMES: Record<ReaderTheme, MermaidConfig['themeVariables']> = {
  dark: {
    darkMode: true,
    background: '#0a0a0a',
    edgeLabelBackground: '#0a0a0a',
    primaryColor: '#111110',
    primaryTextColor: '#f2f0ea',
    primaryBorderColor: '#3a3936',
    secondaryColor: '#161615',
    tertiaryColor: '#0d0d0c',
    lineColor: '#8d8b84',
    textColor: '#bdbbb3',
    noteBkgColor: '#1a1814',
    noteTextColor: '#f2f0ea',
    noteBorderColor: '#f2c230',
    fontFamily: FONT,
  },
  light: {
    background: '#f3f1ea',
    edgeLabelBackground: '#f3f1ea',
    primaryColor: '#ffffff',
    primaryTextColor: '#171614',
    primaryBorderColor: '#b9b3a5',
    secondaryColor: '#ebe8df',
    tertiaryColor: '#f7f5ef',
    lineColor: '#66635c',
    textColor: '#45433e',
    noteBkgColor: '#fbf3dc',
    noteTextColor: '#171614',
    noteBorderColor: '#8a6400',
    fontFamily: FONT,
  },
};

/**
 * Draws the ```mermaid / ```mmd blocks of a post (`figure.diagram > pre.mermaid`) as SVG.
 * Mermaid is only downloaded when a post has a diagram. The source is kept per figure so the
 * diagrams can be drawn again when the reader switches between light and dark.
 */
@Injectable({ providedIn: 'root' })
export class DiagramService {
  private readonly sources = new WeakMap<Element, string>();
  private count = 0;

  async render(host: HTMLElement, theme: ReaderTheme): Promise<void> {
    const figures = Array.from(host.querySelectorAll('figure.diagram'));
    if (!figures.length) return;

    // mermaid sizes the boxes from the text it measures, so the font has to be there first
    const [{ default: mermaid }] = await Promise.all([import('mermaid'), document.fonts.load("16px 'JetBrains Mono'")]);
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: 'base',
      themeVariables: THEMES[theme],
      // text is measured with this, so it has to match the font the labels are drawn in
      fontFamily: FONT,
      // short labels stay on one line; break long ones yourself with <br/>
      flowchart: { wrappingWidth: 400 },
    });

    for (const figure of figures) {
      let source = this.sources.get(figure);
      if (source === undefined) {
        source = figure.querySelector('pre.mermaid')?.textContent ?? '';
        this.sources.set(figure, source);
      }
      try {
        const { svg } = await mermaid.render(`diagram-${++this.count}`, source);
        figure.innerHTML = svg;
        figure.classList.remove('diagram-error');
      } catch {
        // leave the source readable rather than mermaid's error graphic
        figure.classList.add('diagram-error');
        figure.innerHTML = '';
        const pre = document.createElement('pre');
        pre.className = 'mermaid';
        pre.textContent = source;
        figure.append(pre);
      }
    }
  }
}
