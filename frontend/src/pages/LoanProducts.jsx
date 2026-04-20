import { useCallback, useEffect, useState } from 'react';
import { productApi } from '../features/products/api/productApi';
import { formatCurrency, formatPercent, parseTenors } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import Modal from '../components/Modal';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

const PRODUCT_TYPES = [
  'PersonalUnsecured',
  'PersonalSecured',
  'Mortgage',
  'AutoLoan',
  'BusinessLoan',
  'Overdraft',
];

const INTEREST_METHODS = ['Flat', 'ReducingBalance'];
const REPAYMENT_FREQUENCIES = ['Monthly'];
const STATUS_OPTIONS = ['Active', 'Inactive'];
const CARD_GRADIENTS = ['#3B82F6', '#0F766E', '#D97706', '#5B7CE2'];

function normalizeTenors(product) {
  if (Array.isArray(product?.allowedTenors)) {
    return product.allowedTenors;
  }

  return parseTenors(product?.allowedTenorsJson);
}

function createInitialForm(product) {
  const tenors = normalizeTenors(product);

  return {
    productName: product?.productName || '',
    minAmount: product?.minAmount ?? '',
    maxAmount: product?.maxAmount ?? '',
    tenorInput: tenors.length ? tenors.join(', ') : '',
    monthlyInterestRate: product?.monthlyInterestRate ?? '',
    processingFee: product?.processingFee ?? '',
    repaymentFrequency: product?.repaymentFrequency || 'Monthly',
    status: product?.status || 'Active',
    productType: product?.productType || 'PersonalUnsecured',
    interestMethod: product?.interestMethod || 'Flat',
    gracePeriodMonths: product?.gracePeriodMonths ?? 0,
    requiresCollateral: product?.requiresCollateral ?? false,
    maxLtvRatio: product?.maxLtvRatio ?? '',
    earlySettlementFeeRate: product?.earlySettlementFeeRate ?? 0,
    lateFeeRate: product?.lateFeeRate ?? 0,
    lateFeeGraceDays: product?.lateFeeGraceDays ?? 0,
    promotionalRate: product?.promotionalRate ?? '',
    promotionalMonths: product?.promotionalMonths ?? 0,
    description: product?.description || '',
  };
}

function LoanProductModal({ product, onClose, onSaved }) {
  const isEdit = Boolean(product);
  const { toast } = useToast();
  const [form, setForm] = useState(() => createInitialForm(product));
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const setField = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const buildPayload = () => {
    const allowedTenors = form.tenorInput
      .split(',')
      .map((item) => Number.parseInt(item.trim(), 10))
      .filter((value) => Number.isInteger(value) && value > 0);

    if (!form.productName.trim()) {
      throw new Error('Ten san pham la bat buoc.');
    }

    if (!allowedTenors.length) {
      throw new Error('Can it nhat mot ky han hop le.');
    }

    const payload = {
      productName: form.productName.trim(),
      minAmount: Number(form.minAmount || 0),
      maxAmount: Number(form.maxAmount || 0),
      allowedTenors,
      monthlyInterestRate: Number(form.monthlyInterestRate || 0),
      processingFee: Number(form.processingFee || 0),
      status: form.status,
      productType: form.productType,
      interestMethod: form.interestMethod,
      gracePeriodMonths: Number(form.gracePeriodMonths || 0),
      requiresCollateral: Boolean(form.requiresCollateral),
      maxLtvRatio: form.maxLtvRatio === '' ? null : Number(form.maxLtvRatio),
      earlySettlementFeeRate: Number(form.earlySettlementFeeRate || 0),
      lateFeeRate: Number(form.lateFeeRate || 0),
      lateFeeGraceDays: Number(form.lateFeeGraceDays || 0),
      promotionalRate: form.promotionalRate === '' ? null : Number(form.promotionalRate),
      promotionalMonths: Number(form.promotionalMonths || 0),
      description: form.description.trim() || null,
    };

    if (!isEdit) {
      payload.repaymentFrequency = form.repaymentFrequency;
    }

    return payload;
  };

  const handleSubmit = async () => {
    setError('');
    setLoading(true);

    try {
      const payload = buildPayload();

      if (isEdit) {
        await productApi.update(product.id, payload);
        toast('Cap nhat san pham vay thanh cong.');
      } else {
        await productApi.create(payload);
        toast('Tao san pham vay thanh cong.');
      }

      onSaved();
    } catch (nextError) {
      setError(nextError.message || 'Khong the luu san pham.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={isEdit ? 'Cap nhat san pham vay' : 'Tao san pham vay'}
      onClose={onClose}
      size="xl"
      actions={
        <>
          <Button variant="ghost" onClick={onClose}>
            Huy
          </Button>
          <Button onClick={handleSubmit} loading={loading}>
            {isEdit ? 'Cap nhat' : 'Tao moi'}
          </Button>
        </>
      }
    >
      <div className="form-row">
        <Input
          label="Ten san pham *"
          value={form.productName}
          onChange={(event) => setField('productName', event.target.value)}
          placeholder="Vay tieu dung uu tien"
        />
        <Input
          type="select"
          label="Trang thai"
          value={form.status}
          onChange={(event) => setField('status', event.target.value)}
        >
          {STATUS_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Input>
      </div>

      <div className="form-row">
        <Input
          label="So tien toi thieu"
          type="number"
          value={form.minAmount}
          onChange={(event) => setField('minAmount', event.target.value)}
        />
        <Input
          label="So tien toi da"
          type="number"
          value={form.maxAmount}
          onChange={(event) => setField('maxAmount', event.target.value)}
        />
      </div>

      <div className="form-row">
        <Input
          label="Lai suat thang"
          type="number"
          step="0.0001"
          value={form.monthlyInterestRate}
          onChange={(event) => setField('monthlyInterestRate', event.target.value)}
        />
        <Input
          label="Phi xu ly"
          type="number"
          value={form.processingFee}
          onChange={(event) => setField('processingFee', event.target.value)}
        />
      </div>

      <div className="form-row">
        <Input
          label="Ky han (vi du: 6, 12, 24)"
          value={form.tenorInput}
          onChange={(event) => setField('tenorInput', event.target.value)}
        />
        <Input
          type="select"
          label="Tan suat tra no"
          value={form.repaymentFrequency}
          disabled={isEdit}
          onChange={(event) => setField('repaymentFrequency', event.target.value)}
        >
          {REPAYMENT_FREQUENCIES.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Input>
      </div>

      <div className="form-row">
        <Input
          type="select"
          label="Loai san pham"
          value={form.productType}
          onChange={(event) => setField('productType', event.target.value)}
        >
          {PRODUCT_TYPES.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Input>
        <Input
          type="select"
          label="Cach tinh lai"
          value={form.interestMethod}
          onChange={(event) => setField('interestMethod', event.target.value)}
        >
          {INTEREST_METHODS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Input>
      </div>

      <div className="form-row">
        <Input
          label="Grace period (thang)"
          type="number"
          value={form.gracePeriodMonths}
          onChange={(event) => setField('gracePeriodMonths', event.target.value)}
        />
        <Input
          label="Late fee grace days"
          type="number"
          value={form.lateFeeGraceDays}
          onChange={(event) => setField('lateFeeGraceDays', event.target.value)}
        />
      </div>

      <div className="form-row">
        <Input
          label="Phi tat toan som"
          type="number"
          step="0.0001"
          value={form.earlySettlementFeeRate}
          onChange={(event) => setField('earlySettlementFeeRate', event.target.value)}
        />
        <Input
          label="Late fee rate"
          type="number"
          step="0.0001"
          value={form.lateFeeRate}
          onChange={(event) => setField('lateFeeRate', event.target.value)}
        />
      </div>

      <div className="form-row">
        <Input
          label="Promotional rate"
          type="number"
          step="0.0001"
          value={form.promotionalRate}
          onChange={(event) => setField('promotionalRate', event.target.value)}
        />
        <Input
          label="Promotional months"
          type="number"
          value={form.promotionalMonths}
          onChange={(event) => setField('promotionalMonths', event.target.value)}
        />
      </div>

      <div className="form-row">
        <Input
          label="Max LTV ratio"
          type="number"
          step="0.01"
          value={form.maxLtvRatio}
          onChange={(event) => setField('maxLtvRatio', event.target.value)}
          disabled={!form.requiresCollateral}
        />
        <div className="input-u-group">
          <label className="input-u-label">Collateral</label>
          <label className="product-checkbox">
            <input
              type="checkbox"
              checked={form.requiresCollateral}
              onChange={(event) => setField('requiresCollateral', event.target.checked)}
            />
            <span>Requires collateral</span>
          </label>
        </div>
      </div>

      <Input
        type="textarea"
        label="Mo ta"
        rows={4}
        value={form.description}
        onChange={(event) => setField('description', event.target.value)}
        placeholder="Mo ta ngan cho san pham vay."
      />

      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

export default function LoanProducts() {
  const { can } = useAuth();
  const { toast } = useToast();
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState(null);

  const loadProducts = useCallback(async () => {
    setLoading(true);

    try {
      const response = await productApi.list();
      setProducts(Array.isArray(response) ? response : []);
    } catch (error) {
      toast(error.message || 'Khong the tai danh sach san pham.', 'error');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  const handleSaved = async () => {
    setModal(null);
    await loadProducts();
  };

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>San pham vay</h2>
          <p className="subtitle">Quan ly danh muc san pham va cap nhat rule tren giao dien.</p>
        </div>
        <div className="loan-product-toolbar">
          {can.createProduct && (
            <Button onClick={() => setModal({})}>
              Tao san pham
            </Button>
          )}
        </div>
      </div>

      {loading ? (
        <div className="products-grid">
          {[1, 2, 3].map((item) => (
            <div key={item} className="loading-shimmer" style={{ height: 260, borderRadius: 14 }} />
          ))}
        </div>
      ) : !products.length ? (
        <div className="product-empty-state">
          Chua co san pham vay nao.
        </div>
      ) : (
        <div className="products-grid">
          {products.map((product, index) => {
            const tenors = normalizeTenors(product);
            const color = CARD_GRADIENTS[index % CARD_GRADIENTS.length];
            const accent = CARD_GRADIENTS[(index + 1) % CARD_GRADIENTS.length];

            return (
              <div key={product.id} className="product-card">
                <div
                  className="product-header"
                  style={{ background: `linear-gradient(135deg, ${color}22, ${accent}12)` }}
                >
                  <div>
                    <span className="product-name">{product.productName}</span>
                    <div className="product-meta-row">
                      <span className="cell-sub">{product.productType}</span>
                      <span className="cell-sub">{product.interestMethod}</span>
                    </div>
                  </div>
                  <StatusBadge status={product.status || 'Active'} />
                </div>
                <div className="product-body">
                  <div className="product-stat-grid">
                    <div className="product-stat">
                      <div className="stat-label">Toi thieu</div>
                      <div className="stat-value">{formatCurrency(product.minAmount)}</div>
                    </div>
                    <div className="product-stat">
                      <div className="stat-label">Toi da</div>
                      <div className="stat-value">{formatCurrency(product.maxAmount)}</div>
                    </div>
                    <div className="product-stat">
                      <div className="stat-label">Lai suat/thang</div>
                      <div className="stat-value highlight">{formatPercent(product.monthlyInterestRate)}</div>
                    </div>
                    <div className="product-stat">
                      <div className="stat-label">Phi xu ly</div>
                      <div className="stat-value">{formatCurrency(product.processingFee)}</div>
                    </div>
                  </div>

                  <div className="product-tenors">
                    <span className="stat-label">Ky han:</span>
                    {tenors.map((tenor) => (
                      <span key={tenor} className="tenor-chip">
                        {tenor}M
                      </span>
                    ))}
                  </div>

                  <div className="product-meta-row product-meta-row-spaced">
                    <span className="cell-sub">Late fee: {formatPercent(product.lateFeeRate || 0)}</span>
                    <span className="cell-sub">Early settle: {formatPercent(product.earlySettlementFeeRate || 0)}</span>
                  </div>

                  {product.description && (
                    <p className="product-description">{product.description}</p>
                  )}

                  {can.updateProduct && (
                    <div className="product-card-actions">
                      <Button size="sm" variant="outline" onClick={() => setModal({ product })}>
                        Cap nhat
                      </Button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {modal && (
        <LoanProductModal
          product={modal.product}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
    </div>
  );
}
