// Factory query key DUY NHẤT của web — không viết mảng key tay ở nơi khác.
export const queryKeys = {
  menu: ['menu'] as const,
  shift: {
    /** Ca theo cashier (nguồn chân lý của mở/đóng ca). */
    current: ['shift', 'current'] as const,
  },
  orders: {
    all: ['orders'] as const,
    list: (shiftId: string) => ['orders', 'list', shiftId] as const,
    detail: (id: string) => ['orders', 'detail', id] as const,
    /** Ca mà ordering đã biết (projection qua Kafka) — quyết định đã tạo đơn được chưa. */
    currentShift: ['orders', 'current-shift'] as const,
  },
  kitchen: {
    orders: ['kitchen', 'orders'] as const,
  },
}
