import { defineConfig } from 'eslint/config'
import reactHooks from 'eslint-plugin-react-hooks'
import tseslint from 'typescript-eslint'

export default defineConfig(
  { ignores: ['dist', 'src/routeTree.gen.ts', 'playwright-report', 'test-results'] },
  tseslint.configs.recommended,
  reactHooks.configs.flat['recommended-latest'],
  {
    // Luật dự án: mọi lời gọi HTTP đi qua apiClient (tự đính JWT, tự refresh, map ProblemDetails).
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/lib/apiClient.ts', 'src/**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-globals': ['error', { name: 'fetch', message: 'Gọi API qua src/lib/apiClient.ts.' }],
    },
  },
)
