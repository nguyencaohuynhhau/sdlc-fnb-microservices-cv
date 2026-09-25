import { create } from 'zustand'

// CHỈ UI state. Đơn nháp là thứ thu ngân đang soạn trên máy — chưa phải dữ liệu server.
export type CartLine = { menuItemId: string; name: string; price: number; qty: number }

type CartState = {
  lines: CartLine[]
  add: (item: { id: string; name: string; price: number }) => void
  decrement: (menuItemId: string) => void
  clear: () => void
}

export const useCart = create<CartState>((set) => ({
  lines: [],
  add: (item) =>
    set(({ lines }) =>
      lines.some((l) => l.menuItemId === item.id)
        ? { lines: lines.map((l) => (l.menuItemId === item.id ? { ...l, qty: l.qty + 1 } : l)) }
        : { lines: [...lines, { menuItemId: item.id, name: item.name, price: item.price, qty: 1 }] },
    ),
  decrement: (menuItemId) =>
    set(({ lines }) => ({
      lines: lines.flatMap((l) => (l.menuItemId !== menuItemId ? [l] : l.qty > 1 ? [{ ...l, qty: l.qty - 1 }] : [])),
    })),
  clear: () => set({ lines: [] }),
}))
