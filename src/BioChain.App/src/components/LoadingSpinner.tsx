interface Props {
  text?: string;
}

export function LoadingSpinner({ text }: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12">
      <div className="w-8 h-8 border-2 border-accent-primary/30 border-t-accent-primary rounded-full animate-spin" />
      {text && <p className="text-sm text-text-secondary">{text}</p>}
    </div>
  );
}
