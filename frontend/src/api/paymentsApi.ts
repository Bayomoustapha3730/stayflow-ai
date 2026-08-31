import type { HttpClient } from "./httpClient";
import type {
  InitiateMpesaPaymentRequest,
  Payment,
  ReservationPaymentSummary
} from "../models/payments";

export function createPaymentsApi(http: HttpClient) {
  return {
    initiateMpesaPayment(request: InitiateMpesaPaymentRequest) {
      return http.post<Payment>(
        "/api/payments/mpesa/stk",
        request
      );
    },

    getPayment(paymentId: string) {
      return http.get<Payment>(
        `/api/payments/${encodeURIComponent(paymentId)}`
      );
    },

    listReservationPayments(reservationId: string) {
      return http.get<Payment[]>(
        `/api/reservations/${encodeURIComponent(reservationId)}/payments`
      );
    },

    getReservationPaymentSummary(reservationId: string) {
      return http.get<ReservationPaymentSummary>(
        `/api/reservations/${encodeURIComponent(reservationId)}/payment-summary`
      );
    }
  };
}
