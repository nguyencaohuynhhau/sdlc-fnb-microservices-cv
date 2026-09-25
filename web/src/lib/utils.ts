import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

const vnd = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })
export const formatVnd = (n: number) => vnd.format(n)
