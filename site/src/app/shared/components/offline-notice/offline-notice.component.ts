import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

const JOKE_REQUESTS = [
  'GET /api/vibes?check=true',
  'GET /api/is-anyone-home',
  'POST /api/coffee?size=large',
  'GET /api/wake-up?gently=true',
  'GET /api/please?ask=nicely',
];

/**
 * Shown wherever the API can't be reached: a request packet leaves the site,
 * runs down the wire, hits the break and fizzles. Frozen under reduced motion.
 * The request line is made up: it never shows real endpoints or where the API runs.
 */
@Component({
  selector: 'app-offline-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './offline-notice.component.html',
  styleUrl: './offline-notice.component.css',
  host: { class: 'block @container' },
})
export class OfflineNoticeComponent {
  readonly request = input(JOKE_REQUESTS[Math.floor(Math.random() * JOKE_REQUESTS.length)]);
  readonly error = input('connection refused · the server is touching grass');
  readonly retryable = input(true);
  readonly retry = output<void>();
}
