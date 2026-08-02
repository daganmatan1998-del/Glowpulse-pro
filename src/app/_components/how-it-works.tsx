import Image from "next/image";

const steps = [
  {
    n: "01",
    title: "Fill & Close",
    desc: "Pour in your coffee, cocoa, or shake, then snap the lid shut.",
  },
  {
    n: "02",
    title: "Tap the Display",
    desc: "One tap on the handle's touch screen starts the magnetic stirrer.",
  },
  {
    n: "03",
    title: "Swirl in 3 Seconds",
    desc: "Watch it blend perfectly — smooth and lump-free, every time.",
  },
];

export function HowItWorks() {
  return (
    <section id="how-it-works" className="bg-muted/60 py-20">
      <div className="mx-auto grid max-w-6xl grid-cols-1 items-center gap-12 px-4 sm:px-6 lg:grid-cols-2 lg:px-8">
        <div className="order-2 lg:order-1">
          <h2 className="font-heading text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
            From Pour to Swirl in Three Steps
          </h2>
          <ol className="mt-10 space-y-8">
            {steps.map((s) => (
              <li key={s.n} className="flex gap-5">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary font-heading text-sm font-bold text-primary-foreground">
                  {s.n}
                </span>
                <div>
                  <h3 className="font-heading text-lg font-bold text-foreground">
                    {s.title}
                  </h3>
                  <p className="mt-1 text-muted-foreground">{s.desc}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>

        <div className="order-1 overflow-hidden rounded-[2rem] border border-border shadow-xl lg:order-2">
          <Image
            src="/product/lcd-macro.jpg"
            alt="Close-up of the touch-screen display on the mug handle showing live temperature"
            width={960}
            height={960}
            className="h-full w-full object-cover"
          />
        </div>
      </div>
    </section>
  );
}
