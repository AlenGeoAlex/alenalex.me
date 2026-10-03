import { InjectionToken } from '@angular/core';
import { environment } from '@env/environment';

/** Base URL of the API (api.alenalex.me in production, localhost:8080 in dev). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});
