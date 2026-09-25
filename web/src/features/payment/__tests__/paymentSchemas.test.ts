import { expect, test } from 'vitest'
import { PaymentReceiptSchema } from '../paymentSchemas'

// Body thật từ `POST /api/payments` (smoke test qua gateway).
const receipt = {
  paymentId: '6f1c2a3e-0b7d-4e0a-9b1f-2d3c4e5f6a7b',
  orderId: '0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d',
  shiftId: '1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e',
  amount: 90000.0,
  method: 'Cash',
  paidAt: '2026-09-25T09:30:00+00:00',
}

test('paymentSchemas_RealResponse_Parses', () => {
  expect(PaymentReceiptSchema.parse(receipt)).toEqual(receipt)
})

test('paymentSchemas_AmountAsString_Rejected', () => {
  expect(PaymentReceiptSchema.safeParse({ ...receipt, amount: '90000.00' }).success).toBe(false)
})

test('paymentSchemas_UnknownMethod_Rejected', () => {
  expect(PaymentReceiptSchema.safeParse({ ...receipt, method: 'Card' }).success).toBe(false)
})
