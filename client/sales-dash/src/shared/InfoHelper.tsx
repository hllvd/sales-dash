import React, { useState, useRef, useEffect } from 'react';
import './InfoHelper.css';

interface InfoHelperProps {
  label?: string;
  icon?: React.ReactNode;
  children?: React.ReactNode;
  defaultExpanded?: boolean;
}

const InfoHelper: React.FC<InfoHelperProps> = ({
  label,
  icon,
  children,
  defaultExpanded = false
}) => {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const containerRef = useRef<HTMLDivElement>(null);

  // Close when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setExpanded(false);
      }
    };

    if (expanded) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [expanded]);

  return (
    <div className="info-helper-container" ref={containerRef}>
      <button 
        type="button" 
        className={`info-helper-trigger ${expanded ? 'expanded' : ''}`}
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setExpanded(!expanded);
        }}
      >
        <span className="info-helper-icon">
          {icon || (
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
              <line x1="12" y1="17" x2="12.01" y2="17"></line>
            </svg>
          )}
        </span>
        {label && <span className="info-helper-label">{label}</span>}
      </button>
      
      {expanded && children && (
        <div className="info-helper-content">
          {children}
        </div>
      )}
    </div>
  );
};

export default InfoHelper;
