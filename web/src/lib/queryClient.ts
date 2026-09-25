import { MutationCache, QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ApiError, errorMessage } from './apiClient'

export const queryClient: QueryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1 },
  },
  mutationCache: new MutationCache({
    onError: (error) => {
      toast.error(errorMessage(error))
      // 409 = dữ liệu trên máy đã cũ (người khác vừa sửa đơn / ca vừa đổi) → tải lại.
      if (error instanceof ApiError && error.status === 409) void queryClient.invalidateQueries()
    },
  }),
})
