import { formatNumber } from '../utils/format';

export default function Pagination({ paged, onPage }) {
  if (!paged || paged.totalPages <= 1) return null;

  const { page, totalPages, hasPreviousPage, hasNextPage, totalCount } = paged;
  const start = Math.max(1, page - 3);
  const end   = Math.min(totalPages, page + 3);
  const pages = [];
  for (let i = start; i <= end; i++) pages.push(i);

  return (
    <div className="pagination">
      <button disabled={!hasPreviousPage} onClick={() => onPage(page - 1)}>←</button>
      {pages.map(p => (
        <button key={p} className={p === page ? 'active' : ''} onClick={() => onPage(p)}>{p}</button>
      ))}
      <button disabled={!hasNextPage} onClick={() => onPage(page + 1)}>→</button>
      <span className="page-info">{page}/{totalPages} ({formatNumber(totalCount || 0)} bản ghi)</span>
    </div>
  );
}
