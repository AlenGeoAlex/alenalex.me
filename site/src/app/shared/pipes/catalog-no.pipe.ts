import { Pipe, PipeTransform } from '@angular/core';

/** 42 | catalogNo: 'GB' : 4  →  "GB 0042" */
@Pipe({ name: 'catalogNo' })
export class CatalogNoPipe implements PipeTransform {
  transform(n: number, prefix: string, width = 3): string {
    return `${prefix} ${String(n).padStart(width, '0')}`;
  }
}
