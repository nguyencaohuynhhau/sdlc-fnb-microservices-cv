import { useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from '@/lib/apiClient'
import { queryKeys } from '@/lib/queryKeys'
import { MenuItemSchema } from '@/features/orders/orderSchemas'

export function useMenu() {
  return useQuery({
    queryKey: queryKeys.menu,
    queryFn: async () => z.array(MenuItemSchema).parse(await api('/api/menu')),
  })
}
