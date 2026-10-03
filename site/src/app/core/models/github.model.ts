export interface ContributionDay {
  date: string;
  count: number;
}

export interface ContributionWeek {
  days: ContributionDay[];
}

export interface Repo {
  name: string;
  description: string | null;
  url: string;
  stars: number;
  language: string | null;
  topics: string[];
  pushedAt: string;
  createdAt: string;
  fork: boolean;
}

/** GET /api/github */
export interface GithubSummary {
  login: string;
  publicRepos: number;
  totalStars: number;
  topLanguage: string | null;
  lastPushAt: string | null;
  contributions: { total: number; weeks: ContributionWeek[] };
  repos: Repo[];
  fetchedAt: string;
}
