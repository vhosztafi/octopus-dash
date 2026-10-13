interface CloudflareEnv {
  LOGIN_RATE_LIMITER: {
    limit(options: { key: string }): Promise<{ success: boolean }>;
  };
}
