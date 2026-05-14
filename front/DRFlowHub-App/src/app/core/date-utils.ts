export function toDateInputValue(value: string | Date | null | undefined, fallback = ''): string {
  if (!value) {
    return fallback;
  }

  if (typeof value === 'string') {
    const dateOnly = value.match(/^(\d{4}-\d{2}-\d{2})/);
    if (dateOnly) {
      return dateOnly[1];
    }
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}
