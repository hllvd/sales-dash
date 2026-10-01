import React from 'react';
import './Pagination.css';

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  pageSize: number;
  totalItems: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  pageSizeOptions?: number[];
  showTopControls?: boolean;
  showBottomControls?: boolean;
}

const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalPages,
  pageSize,
  totalItems,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [50, 100, 500],
  showTopControls = true,
  showBottomControls = true,
}) => {
  const handlePrevious = () => {
    if (currentPage > 1) {
      onPageChange(currentPage - 1);
    }
  };

  const handleNext = () => {
    if (currentPage < totalPages) {
      onPageChange(currentPage + 1);
    }
  };

  const TopControls = () => (
    <div className="pagination-container">
      <div className="pagination-nav">
        <button
          className="pagination-btn"
          onClick={handlePrevious}
          disabled={currentPage === 1}
        >
          ← Anterior
        </button>
        <span className="pagination-info">
          <span className="pagination-info-pages">
            Página {currentPage} de {totalPages}
          </span>
          <span className="pagination-info-total">
            ({totalItems} total)
          </span>
        </span>
        <button
          className="pagination-btn"
          onClick={handleNext}
          disabled={currentPage === totalPages}
        >
          Próxima →
        </button>
      </div>
      <div className="pagination-page-size">
        <span className="pagination-page-size-label">Itens por página:</span>
        {pageSizeOptions.map(size => (
          <button
            key={size}
            onClick={() => onPageSizeChange(size)}
            className={`pagination-size-btn ${pageSize === size ? 'active' : ''}`}
          >
            {size}
          </button>
        ))}
      </div>
    </div>
  );

  const BottomControls = () => (
    <div className="pagination-bottom-container">
      <button
        className="pagination-btn"
        onClick={handlePrevious}
        disabled={currentPage === 1}
      >
        ← Anterior
      </button>
      <span className="pagination-info">
        <span className="pagination-info-pages">
          Página {currentPage} de {totalPages}
        </span>
      </span>
      <button
        className="pagination-btn"
        onClick={handleNext}
        disabled={currentPage === totalPages}
      >
        Próxima →
      </button>
    </div>
  );

  return (
    <>
      {showTopControls && <TopControls />}
      {showBottomControls && <BottomControls />}
    </>
  );
};

export default Pagination;
