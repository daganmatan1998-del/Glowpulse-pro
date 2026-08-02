"use client";

import { useState } from "react";
import {
  PayPalScriptProvider,
  PayPalButtons,
  type PayPalButtonsComponentProps,
} from "@paypal/react-paypal-js";
import { CheckCircle2, AlertCircle } from "lucide-react";

import { product } from "@/lib/product";

// PayPal's own public sandbox client ID — renders working Smart Buttons
// against PayPal's test environment so the page never ships broken, but no
// real money moves until NEXT_PUBLIC_PAYPAL_CLIENT_ID is set to a live
// client ID from https://developer.paypal.com.
const PAYPAL_CLIENT_ID = process.env.NEXT_PUBLIC_PAYPAL_CLIENT_ID || "sb";
const isSandbox = PAYPAL_CLIENT_ID === "sb";

export function CheckoutButtons() {
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");

  const createOrder: PayPalButtonsComponentProps["createOrder"] = (
    _data,
    actions
  ) =>
    actions.order.create({
      intent: "CAPTURE",
      purchase_units: [
        {
          description: product.fullName,
          amount: {
            currency_code: "USD",
            value: product.priceUSD,
          },
        },
      ],
    });

  const onApprove: PayPalButtonsComponentProps["onApprove"] = async (
    _data,
    actions
  ) => {
    if (!actions.order) return;
    await actions.order.capture();
    setStatus("success");
  };

  if (status === "success") {
    return (
      <div className="flex items-start gap-3 rounded-xl bg-primary/10 p-4 text-sm text-foreground">
        <CheckCircle2 size={20} className="mt-0.5 shrink-0 text-primary" aria-hidden />
        <div>
          <p className="font-semibold">Order placed!</p>
          <p className="mt-1 text-muted-foreground">
            You&apos;ll get a payment receipt from PayPal shortly.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div>
      {isSandbox && (
        <div className="mb-3 flex items-start gap-2 rounded-xl bg-secondary/15 p-3 text-xs text-secondary">
          <AlertCircle size={16} className="mt-0.5 shrink-0" aria-hidden />
          <span>
            Test mode: this uses PayPal&apos;s sandbox, no real payment will
            be taken. Set NEXT_PUBLIC_PAYPAL_CLIENT_ID to go live.
          </span>
        </div>
      )}
      <PayPalScriptProvider
        options={{
          clientId: PAYPAL_CLIENT_ID,
          currency: "USD",
          components: "buttons",
          enableFunding: "card",
        }}
      >
        <PayPalButtons
          style={{ layout: "vertical", shape: "pill", label: "pay" }}
          createOrder={createOrder}
          onApprove={onApprove}
          onError={() => setStatus("error")}
        />
      </PayPalScriptProvider>
      {status === "error" && (
        <p className="mt-3 text-sm text-red-600">
          Something went wrong with that payment. Please try again.
        </p>
      )}
    </div>
  );
}
