import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { POST_LOADER, PostLoader } from '@core/tokens/post-loader.token';
import { Post } from '@core/models/post.model';

/** At prerender time, posts are read straight from the files tools/build-content.mjs generated. */
const readPostFromDisk: PostLoader = async (path) => {
  try {
    const file = join(process.cwd(), 'public', 'content', 'posts', `${path}.json`);
    return JSON.parse(await readFile(file, 'utf8')) as Post;
  } catch {
    return null;
  }
};

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    { provide: POST_LOADER, useValue: readPostFromDisk },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
