import { useState, useEffect } from 'react';
import { productApi } from '../features/products/api/productApi';
import { formatCurrency, formatPercent, parseTenors } from '../utils/format';
import { StatusBadge } from '../components/Badge';

export default function LoanProducts() {
  const [products, setProducts] = useState([]);
  const [loading, setLoading]   = useState(true);

  useEffect(() => {
    productApi.list().then(d => setProducts(Array.isArray(d) ? d : [])).catch(console.error).finally(() => setLoading(false));
  }, []);

  const COLORS = ['#3B82F6','#8B5CF6','#10B981'];

  return (
    <div className="page">
      <div className="page-header">
        <h2>📦 Sản phẩm vay</h2>
      </div>

      {loading ? (
        <div className="products-grid">
          {[1,2,3].map(i => <div key={i} className="loading-shimmer" style={{height:220,borderRadius:14}} />)}
        </div>
      ) : !products.length ? (
        <div style={{textAlign:'center',padding:'3rem',color:'var(--text-muted)',border:'1px dashed var(--border)',borderRadius:'var(--radius)'}}>
          Chưa có sản phẩm vay nào
        </div>
      ) : (
        <div className="products-grid">
          {products.map((p, i) => (
            <div key={p.id} className="product-card">
              <div className="product-header" style={{background:`linear-gradient(135deg,${COLORS[i%3]}22,${COLORS[(i+1)%3]}11)`}}>
                <span className="product-name">{p.productName}</span>
                <StatusBadge status={p.status || 'Active'} />
              </div>
              <div className="product-body">
                <div className="product-stat-grid">
                  <div className="product-stat"><div className="stat-label">Tối thiểu</div><div className="stat-value">{formatCurrency(p.minAmount)}</div></div>
                  <div className="product-stat"><div className="stat-label">Tối đa</div><div className="stat-value">{formatCurrency(p.maxAmount)}</div></div>
                  <div className="product-stat"><div className="stat-label">Lãi suất/tháng</div><div className="stat-value highlight">{formatPercent(p.monthlyInterestRate)}</div></div>
                  <div className="product-stat"><div className="stat-label">Phí xử lý</div><div className="stat-value">{formatCurrency(p.processingFee)}</div></div>
                </div>
                <div className="product-tenors">
                  <span className="stat-label">Kỳ hạn:</span>
                  {parseTenors(p.allowedTenorsJson).map(t => (
                    <span key={t} className="tenor-chip">{t}M</span>
                  ))}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
