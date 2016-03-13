# == Schema Information
#
# Table name: games
#
#  id      :integer          not null, primary key
#  home_id :integer
#  away_id :integer
#  date    :date
#
# Indexes
#
#  index_games_on_away_id  (away_id)
#  index_games_on_home_id  (home_id)
#

class Game < ActiveRecord::Base

  belongs_to :home, class_name: 'Team'
  belongs_to :away, class_name: 'Team'

  has_many :stats, class_name: 'GameStat'

  has_one :home_stats, -> (inst) { where(team_id: inst.home_id) }, class_name: 'GameStat'
  has_one :away_stats, -> (inst) { where(team_id: inst.away_id) }, class_name: 'GameStat'

  scope :participant, -> (team_id) { where('home_id = :team or away_id = :team', team: team_id) }
end
