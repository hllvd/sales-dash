import React from 'react';

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
    <div className="pagination-controls" style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
        <span style={{ fontSize: '14px', color: '#e9ecef', fontWeight: '500' }}>Itens por página:</span>
        {pageSizeOptions.map(size => (
          <button
            key={size}
            onClick={() => onPageSizeChange(size)}
            style={{
              padding: '0.5rem 1rem',
              border: pageSize === size ? '2px solid #339af0' : '1px solid #868e96',
              background: pageSize === size ? '#1971c2' : '#495057',
              color: 'white',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: pageSize === size ? '600' : '500',
              transition: 'all 0.2s ease'
            }}
          >
            {size}
          </button>
        ))}
      </div>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
        <span style={{ fontSize: '14px', color: '#e9ecef', fontWeight: '500' }}>
          Página {currentPage} de {totalPages} ({totalItems} total)
        </span>
        <button
          onClick={handlePrevious}
          disabled={currentPage === 1}
          style={{
            padding: '0.5rem 1rem',
            border: '1px solid #868e96',
            background: currentPage === 1 ? '#343a40' : '#495057',
            color: currentPage === 1 ? '#868e96' : '#f8f9fa',
            borderRadius: '6px',
            cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
            fontSize: '14px',
            fontWeight: '500',
            transition: 'all 0.2s ease'
          }}
        >
          ← Anterior
        </button>
        <button
          onClick={handleNext}
          disabled={currentPage === totalPages}
          style={{
            padding: '0.5rem 1rem',
            border: '1px solid #868e96',
            background: currentPage === totalPages ? '#343a40' : '#495057',
            color: currentPage === totalPages ? '#868e96' : '#f8f9fa',
            borderRadius: '6px',
            cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
            fontSize: '14px',
            fontWeight: '500',
            transition: 'all 0.2s ease'
          }}
        >
          Próxima →
        </button>
      </div>
    </div>
  );

  const BottomControls = () => (
    <div className="pagination-controls" style={{ marginTop: '1rem', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '0.5rem' }}>
      <button
        onClick={handlePrevious}
        disabled={currentPage === 1}
        style={{
          padding: '0.5rem 1rem',
          border: '1px solid #868e96',
          background: currentPage === 1 ? '#343a40' : '#495057',
          color: currentPage === 1 ? '#868e96' : '#f8f9fa',
          borderRadius: '6px',
          cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
          fontSize: '14px',
          fontWeight: '500',
          transition: 'all 0.2s ease'
        }}
      >
        ← Anterior
      </button>
      <span style={{ fontSize: '14px', color: '#e9ecef', padding: '0 1rem', fontWeight: '500' }}>
        Página {currentPage} de {totalPages}
      </span>
      <button
        onClick={handleNext}
        disabled={currentPage === totalPages}
        style={{
          padding: '0.5rem 1rem',
          border: '1px solid #868e96',
          background: currentPage === totalPages ? '#343a40' : '#495057',
          color: currentPage === totalPages ? '#868e96' : '#f8f9fa',
          borderRadius: '6px',
          cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
          fontSize: '14px',
          fontWeight: '500',
          transition: 'all 0.2s ease'
        }}
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
