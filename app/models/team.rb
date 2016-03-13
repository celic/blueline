# == Schema Information
#
# Table name: teams
#
#  id           :integer          not null, primary key
#  name         :string
#  abbreviation :string
#  city         :string
#  state        :string
#  full_name    :string
#  division_id  :integer
#  wins         :integer          default(0)
#  losses       :integer          default(0)
#  ot           :integer          default(0)
#  sow          :integer          default(0)
#  sol          :integer          default(0)
#  points       :integer          default(0)
#
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

class Team < ActiveRecord::Base

  before_create :generate_full_name

  has_many :players

  has_many :stats, class_name: 'GameStat'
  has_many :games, through: :stats, source: :game

  scope :division, -> (division) { where(division_id: division) }
  scope :standings, -> { select('teams.*, (wins - sow) as row').order('points desc, row desc') }

  def self.by_abbrev(abbv)
    find_by(abbreviation: abbv.to_s.upcase)
  end

  private

  def generate_full_name

    # set default full name if not set
    self.full_name ||= "#{city} #{name}"

    # continue with creation
    true
  end
end
