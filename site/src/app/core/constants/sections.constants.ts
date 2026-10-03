import { Section } from '@core/models/section.model';

export const SECTIONS: readonly Section[] = [
  { no: '000', path: '/', label: '~', object: 'index' },
  { no: '010', path: '/writing', label: 'writing', object: 'writing' },
  { no: '020', path: '/work', label: 'work', object: 'work' },
  { no: '030', path: '/guestbook', label: 'guestbook', object: 'guestbook' },
  { no: '040', path: '/now', label: 'now', object: 'now' },
];
